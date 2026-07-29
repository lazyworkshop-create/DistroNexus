using System.Runtime.InteropServices;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace DistroNexus.Core.Services;

/// <summary>
/// Line-based INI reader/writer for ~/.wslconfig.
/// </summary>
public class WslConfigService : IWslConfigService, IWslConfigurationService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<WslConfigService> _logger;
    private readonly string _userProfileDir;
    private readonly IRecoveryOfferService? _recoveryOffers;

    public WslConfigService(ILogger<WslConfigService> logger, string? userProfileDir = null, IRecoveryOfferService? recoveryOffers = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userProfileDir = userProfileDir
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _recoveryOffers = recoveryOffers;
    }

    private string WslConfigPath => Path.Combine(_userProfileDir, ".wslconfig");

    public Task<RecoveryOffer> GetRecoveryOfferAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new RecoveryOffer(false, "", RecoveryOfferReason.MajorConfigurationChange, "RecoveryOffer.HostConfigurationRequiresInstance"));

    // ── IWslConfigService ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<WslConfig> GetWslConfigAsync(CancellationToken ct = default)
    {
        var document = await ReadAsync(ct);
        var values = document.Settings.Values;
        return new WslConfig
        {
            Memory = Get(values, "wsl2", "memory"),
            Processors = int.TryParse(Get(values, "wsl2", "processors"), out var p) ? p : null,
            Swap = Get(values, "wsl2", "swap"),
            LocalhostForwarding = bool.TryParse(Get(values, "wsl2", "localhostForwarding"), out var lf) ? lf : null,
            NetworkingMode = Get(values, "wsl2", "networkingMode")
        };
    }

    /// <inheritdoc/>
    public async Task SetWslConfigAsync(WslConfig config, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (config.Memory is not null) updates["wsl2.memory"] = config.Memory;
        if (config.Processors.HasValue) updates["wsl2.processors"] = config.Processors.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (config.Swap is not null) updates["wsl2.swap"] = config.Swap;
        if (config.LocalhostForwarding.HasValue) updates["wsl2.localhostForwarding"] = config.LocalhostForwarding.Value.ToString().ToLowerInvariant();
        if (config.NetworkingMode is not null) updates["wsl2.networkingMode"] = config.NetworkingMode;
        var current = await ReadAsync(ct);
        await SaveAsync(updates, current.Fingerprint, null, ct);
    }

    public async Task<ConfigurationDocument<WslConfigurationSettings>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var bytes = File.Exists(WslConfigPath) ? await File.ReadAllBytesAsync(WslConfigPath, cancellationToken) : [];
        var source = LosslessIniDocument.Parse(bytes);
        var values = Project(source);
        var known = WslConfigurationSchema.Global.Select(d => Id(d.Section, d.Key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = source.Tokens.Count(t => t.Kind == ConfigurationTokenKind.KeyValue && !known.Contains(Id(t.Section!, t.Key!)));
        var diagnostics = WslConfigurationSchema.Validate(source, WslConfigurationSchema.Global);
        return new(new(values), source, diagnostics, unknown, Fingerprint(bytes), RestartScope.Wsl, source.ToString());
    }

    public async Task<ConfigurationSaveResult> SaveAsync(IReadOnlyDictionary<string, string?> values,
        string expectedFingerprint, IReadOnlySet<string>? availableCapabilities = null,
        CancellationToken cancellationToken = default)
    {
        var gate = FileLocks.GetOrAdd(Path.GetFullPath(WslConfigPath), _ => new(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var original = File.Exists(WslConfigPath) ? await File.ReadAllBytesAsync(WslConfigPath, cancellationToken) : [];
        if (!string.Equals(Fingerprint(original), expectedFingerprint, StringComparison.Ordinal))
            throw new ConfigurationConflictException(".wslconfig changed after it was loaded; reload before saving.");
        if (values.Count == 0) return new(expectedFingerprint, null, RestartScope.None);
        var edited = LosslessIniDocument.Parse(original);
        foreach (var (id, value) in values)
        {
            var split = id.IndexOf('.');
            if (split <= 0) throw new ArgumentException($"Setting id '{id}' must be section.key.", nameof(values));
            var section = id[..split]; var key = id[(split + 1)..];
            var definition = WslConfigurationSchema.Global.LastOrDefault(d =>
                string.Equals(d.Section, section, StringComparison.OrdinalIgnoreCase) && string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
            if (definition is null) throw new ConfigurationValidationException([new(0, "config.unsupported", $"Unsupported setting {id}.")]);
            if (definition.RequiredCapability is not null && (availableCapabilities is null || !availableCapabilities.Contains(definition.RequiredCapability)))
                throw new ConfigurationValidationException([new(0, "config.unsupported", $"{id} is not supported by this WSL version.")]);
            if (id.Equals("wsl2.networkingMode", StringComparison.OrdinalIgnoreCase) && value?.Equals("mirrored", StringComparison.OrdinalIgnoreCase) == true &&
                (availableCapabilities is null || !availableCapabilities.Contains("wsl.config.mirroredNetworking")))
                throw new ConfigurationValidationException([new(0, "config.unsupported", "Mirrored networking is not supported by this WSL version.")]);
            edited = value is null ? edited.WithoutValue(section, key) : edited.WithValue(section, key, value);
        }
        var diagnostics = WslConfigurationSchema.Validate(edited, WslConfigurationSchema.Global);
        if (diagnostics.Any(d => d.Severity == ConfigurationDiagnosticSeverity.Error)) throw new ConfigurationValidationException(diagnostics);
        var bytes = edited.ToBytes();
        var backup = original.Length == 0 ? null : WslConfigPath + ".bak." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfffffff") + "." + Guid.NewGuid().ToString("N");
        if (backup is not null) await File.WriteAllBytesAsync(backup, original, cancellationToken);
        var temp = WslConfigPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            { await stream.WriteAsync(bytes, cancellationToken); await stream.FlushAsync(cancellationToken); stream.Flush(true); }
            var immediatelyCurrent = File.Exists(WslConfigPath) ? await File.ReadAllBytesAsync(WslConfigPath, cancellationToken) : [];
            if (!string.Equals(Fingerprint(immediatelyCurrent), expectedFingerprint, StringComparison.Ordinal))
                throw new ConfigurationConflictException(".wslconfig changed immediately before replacement; reload before saving.");
            File.Move(temp, WslConfigPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        _logger.LogInformation("Updated .wslconfig at {Path}; backup {BackupPath}", WslConfigPath, backup);
        return new(Fingerprint(bytes), backup, RestartScope.Wsl);
        }
        finally { gate.Release(); }
    }

    public async Task<ConfigurationPreview> PreviewAsync(IReadOnlyDictionary<string, string?> values,
        string expectedFingerprint, IReadOnlySet<string> availableCapabilities, CancellationToken cancellationToken = default)
    {
        var bytes = File.Exists(WslConfigPath) ? await File.ReadAllBytesAsync(WslConfigPath, cancellationToken) : [];
        if (!string.Equals(Fingerprint(bytes), expectedFingerprint, StringComparison.Ordinal))
            throw new ConfigurationConflictException(".wslconfig changed after it was loaded; reload before saving.");
        var edited = Apply(LosslessIniDocument.Parse(bytes), values, availableCapabilities);
        return new(Encoding.UTF8.GetString(bytes.AsSpan(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? 3 : 0)), edited.ToString(), values.Keys.ToArray(), RestartScope.Wsl);
    }

    private static LosslessIniDocument Apply(LosslessIniDocument edited, IReadOnlyDictionary<string, string?> values,
        IReadOnlySet<string> availableCapabilities)
    {
        foreach (var (id, value) in values)
        {
            var split = id.IndexOf('.');
            if (split <= 0) throw new ArgumentException($"Setting id '{id}' must be section.key.", nameof(values));
            var section = id[..split]; var key = id[(split + 1)..];
            var definition = WslConfigurationSchema.Global.LastOrDefault(d => string.Equals(d.Section, section, StringComparison.OrdinalIgnoreCase) && string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
            if (definition is null || definition.RequiredCapability is not null && !availableCapabilities.Contains(definition.RequiredCapability))
                throw new ConfigurationValidationException([new(0, "config.unsupported", $"Unsupported setting {id}.")]);
            if (id.Equals("wsl2.networkingMode", StringComparison.OrdinalIgnoreCase) &&
                value?.Equals("mirrored", StringComparison.OrdinalIgnoreCase) == true &&
                !availableCapabilities.Contains("wsl.config.mirroredNetworking"))
                throw new ConfigurationValidationException([new(0, "config.unsupported", "Mirrored networking is not supported by this WSL version.")]);
            edited = value is null ? edited.WithoutValue(section, key) : edited.WithValue(section, key, value);
        }
        var diagnostics = WslConfigurationSchema.Validate(edited, WslConfigurationSchema.Global);
        if (diagnostics.Any(d => d.Severity == ConfigurationDiagnosticSeverity.Error)) throw new ConfigurationValidationException(diagnostics);
        return edited;
    }

    private static Dictionary<string, string> Project(LosslessIniDocument source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in source.Tokens.Where(t => t.Kind == ConfigurationTokenKind.KeyValue))
            result[Id(token.Section!, token.Key!)] = token.Value!;
        return result;
    }
    private static string? Get(IReadOnlyDictionary<string, string> values, string section, string key) =>
        values.TryGetValue(Id(section, key), out var value) ? value : null;
    private static string Id(string section, string key) => $"{section}.{key}";
    internal static string Fingerprint(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    /// <inheritdoc/>
    public Task<(long TotalRamMb, int CpuCount)> GetHostSpecsAsync(CancellationToken ct = default)
    {
        long ramMb = 0;

        // Use GC as a rough estimate for total managed memory (not perfect but avoids P/Invoke)
        // In practice, Environment.WorkingSet is available cross-platform
        try
        {
            // Read from /proc/meminfo if on Linux host; otherwise fall back
            var gcMemory = GC.GetGCMemoryInfo();
            // TotalAvailableMemoryBytes is available in .NET 5+
            var totalBytes = gcMemory.TotalAvailableMemoryBytes;
            if (totalBytes > 0) ramMb = totalBytes / (1024 * 1024);
        }
        catch
        {
            // swallow — fall through to 0
        }

        int cpuCount = Environment.ProcessorCount;

        return Task.FromResult((ramMb, cpuCount));
    }
}
