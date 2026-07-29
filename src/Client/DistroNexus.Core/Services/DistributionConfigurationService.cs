using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Reads and writes the fixed /etc/wsl.conf boundary without interpolating distribution or values into commands.</summary>
public sealed partial class DistributionConfigurationService(IProcessRunner runner, IRecoveryOfferService? recoveryOffers = null) : IDistributionConfigurationService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InstanceLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public Task<RecoveryOffer> GetRecoveryOfferAsync(string distribution, CancellationToken cancellationToken = default) =>
        recoveryOffers?.GetOfferAsync(distribution, RecoveryOfferReason.MajorConfigurationChange, cancellationToken)
        ?? Task.FromResult(new RecoveryOffer(false, distribution, RecoveryOfferReason.MajorConfigurationChange, "RecoveryOffer.Unavailable"));

    public async Task<ConfigurationDocument<DistributionConfigurationSettings>> ReadAsync(string distribution,
        CancellationToken cancellationToken = default)
    {
        ValidateDistribution(distribution);
        var owned = Path.Combine(Path.GetTempPath(), "DistroNexus", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        try
        {
            var helperPath = Path.Combine(owned, "read-wsl-conf.sh");
            await File.WriteAllTextAsync(helperPath, ReadHelper, new UTF8Encoding(false), cancellationToken);
            var result = await runner.RunAsync(new("wsl.exe", ["--distribution", distribution, "--exec", "/bin/sh", ToWslPath(helperPath)], Timeout), cancellationToken);
            EnsureSuccess(result, "config.read");
            if (result.StandardOutput == "DNX_ABSENT\n" || result.StandardOutput == "DNX_ABSENT\r\n") return Create([]);
            const string marker = "DNX_DATA\n";
            if (!result.StandardOutput.StartsWith(marker, StringComparison.Ordinal))
                throw new ConfigurationTransportException("config.protocol", "The configuration helper returned an invalid response.");
            try { return Create(Convert.FromBase64String(result.StandardOutput[marker.Length..].Trim())); }
            catch (FormatException ex) { throw new ConfigurationTransportException("config.protocol", $"The configuration helper returned invalid encoded data: {ex.Message}"); }
        }
        finally { try { Directory.Delete(owned, true); } catch (IOException) { } }
    }

    public async Task<ConfigurationSaveResult> SaveAsync(string distribution, IReadOnlyDictionary<string, string?> values,
        string expectedFingerprint, CancellationToken cancellationToken = default)
    {
        ValidateDistribution(distribution);
        var gate = InstanceLocks.GetOrAdd(distribution, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        var current = await ReadAsync(distribution, cancellationToken);
        if (!string.Equals(current.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
            throw new ConfigurationConflictException("/etc/wsl.conf changed after it was loaded; reload before saving.");
        if (values.Count == 0) return new(expectedFingerprint, null, RestartScope.None);
        var edited = current.Source;
        foreach (var (id, value) in values)
        {
            var split = id.IndexOf('.');
            if (split <= 0) throw new ArgumentException($"Setting id '{id}' must be section.key.", nameof(values));
            var section = id[..split]; var key = id[(split + 1)..];
            if (!WslConfigurationSchema.Distribution.Any(d => Eq(d.Section, section) && Eq(d.Key, key)))
                throw new ConfigurationValidationException([new(0, "config.unsupported", $"Unsupported setting {id}.")]);
            edited = value is null ? edited.WithoutValue(section, key) : edited.WithValue(section, key, value);
        }
        if (values.TryGetValue("user.default", out var defaultUser) && defaultUser is not null)
        {
            if (!UserRegex().IsMatch(defaultUser))
                throw new ConfigurationValidationException([new(0, "config.invalidUser", "Default user has an invalid Linux account name.")]);
            var account = await runner.RunAsync(new("wsl.exe",
                ["--distribution", distribution, "--exec", "/usr/bin/getent", "passwd", defaultUser], Timeout), cancellationToken);
            if (account.ExitCode != 0 || string.IsNullOrWhiteSpace(account.StandardOutput))
                throw new ConfigurationValidationException([new(0, "config.userNotFound", "Default user does not exist in the distribution.")]);
        }
        var diagnostics = WslConfigurationSchema.Validate(edited, WslConfigurationSchema.Distribution);
        if (diagnostics.Any(d => d.Severity == ConfigurationDiagnosticSeverity.Error)) throw new ConfigurationValidationException(diagnostics);

        // The helper is fixed and accepts only a content file; target path and backup policy cannot be influenced by callers.
        var owned = Path.Combine(Path.GetTempPath(), "DistroNexus", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        var contentPath = Path.Combine(owned, "wsl.conf");
        var helperPath = Path.Combine(owned, "write-wsl-conf.sh");
        try
        {
            await File.WriteAllBytesAsync(contentPath, edited.ToBytes(), cancellationToken);
            await File.WriteAllTextAsync(helperPath, Helper, new UTF8Encoding(false), cancellationToken);
            var linuxHelper = ToWslPath(helperPath); var linuxContent = ToWslPath(contentPath);
            var result = await runner.RunAsync(new("wsl.exe",
                ["--distribution", distribution, "--user", "root", "--exec", "/bin/sh", linuxHelper, linuxContent, expectedFingerprint], Timeout), cancellationToken);
            if (result.ExitCode == 73) throw new ConfigurationConflictException("/etc/wsl.conf changed immediately before replacement; reload before saving.");
            EnsureSuccess(result, "config.write");
            var fingerprint = WslConfigService.Fingerprint(edited.ToBytes());
            var backup = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return new(fingerprint, backup, RestartScope.Instance);
        }
        finally { try { Directory.Delete(owned, true); } catch (IOException) { } }
        }
        finally { gate.Release(); }
    }

    public async Task<ConfigurationPreview> PreviewAsync(string distribution, IReadOnlyDictionary<string, string?> values,
        string expectedFingerprint, CancellationToken cancellationToken = default)
    {
        var current = await ReadAsync(distribution, cancellationToken);
        if (!string.Equals(current.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
            throw new ConfigurationConflictException("/etc/wsl.conf changed after it was loaded; reload before saving.");
        var edited = Apply(current.Source, values);
        return new(current.RawPreview, edited.ToString(), values.Keys.ToArray(), RestartScope.Instance);
    }

    private static LosslessIniDocument Apply(LosslessIniDocument edited, IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (id, value) in values)
        {
            var split = id.IndexOf('.');
            if (split <= 0) throw new ArgumentException($"Setting id '{id}' must be section.key.", nameof(values));
            var section = id[..split]; var key = id[(split + 1)..];
            if (!WslConfigurationSchema.Distribution.Any(d => Eq(d.Section, section) && Eq(d.Key, key)))
                throw new ConfigurationValidationException([new(0, "config.unsupported", $"Unsupported setting {id}.")]);
            edited = value is null ? edited.WithoutValue(section, key) : edited.WithValue(section, key, value);
        }
        var diagnostics = WslConfigurationSchema.Validate(edited, WslConfigurationSchema.Distribution);
        if (diagnostics.Any(d => d.Severity == ConfigurationDiagnosticSeverity.Error)) throw new ConfigurationValidationException(diagnostics);
        return edited;
    }

    private static ConfigurationDocument<DistributionConfigurationSettings> Create(byte[] bytes)
    {
        var source = LosslessIniDocument.Parse(bytes);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in source.Tokens.Where(t => t.Kind == ConfigurationTokenKind.KeyValue))
            values[$"{token.Section}.{token.Key}"] = token.Value!;
        var known = WslConfigurationSchema.Distribution.Select(d => $"{d.Section}.{d.Key}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = source.Tokens.Count(t => t.Kind == ConfigurationTokenKind.KeyValue && !known.Contains($"{t.Section}.{t.Key}"));
        var diagnostics = WslConfigurationSchema.Validate(source, WslConfigurationSchema.Distribution);
        return new(new(values), source, diagnostics, unknown, WslConfigService.Fingerprint(bytes), RestartScope.Instance, source.ToString());
    }

    private static void ValidateDistribution(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || !DistributionRegex().IsMatch(value))
            throw new ArgumentException("Distribution name contains unsupported characters.", nameof(value));
    }
    private static string ToWslPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length < 3 || full[1] != ':') throw new InvalidOperationException("The fixed helper must reside on a drive mounted by WSL.");
        return $"/mnt/{char.ToLowerInvariant(full[0])}/{full[3..].Replace('\\', '/')}";
    }
    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static void EnsureSuccess(ProcessResult result, string operation)
    {
        if (result.Cancelled) throw new OperationCanceledException("The configuration operation was cancelled.");
        if (result.TimedOut) throw new ConfigurationTransportException(operation + ".timeout", "The configuration operation timed out.");
        if (result.OutputTruncated) throw new ConfigurationTransportException(operation + ".truncated", "The configuration helper output exceeded its safety limit.");
        if (result.Failure != ProcessFailureKind.None) throw new ConfigurationTransportException(operation + ".start", "The configuration helper could not be started.");
        if (result.ExitCode != 0) throw new ConfigurationTransportException(operation + ".failed", $"The configuration helper failed with exit code {result.ExitCode}.");
    }
    [GeneratedRegex(@"^[\p{L}\p{N}_. -]+$", RegexOptions.CultureInvariant)] private static partial Regex DistributionRegex();
    [GeneratedRegex(@"^[a-z_][a-z0-9_-]{0,31}$", RegexOptions.CultureInvariant)] private static partial Regex UserRegex();

    private const string Helper = """
#!/bin/sh
set -eu
[ "$#" -eq 2 ] || exit 64
source_file="$1"
expected="$2"
[ -f "$source_file" ] || exit 66
target=/etc/wsl.conf
if [ -e "$target" ]; then current=$(sha256sum "$target" | cut -d ' ' -f 1); else current=$(printf '' | sha256sum | cut -d ' ' -f 1); fi
[ "$(printf '%s' "$current" | tr '[:lower:]' '[:upper:]')" = "$expected" ] || { printf 'DNX_CONFLICT\n'; exit 73; }
timestamp=$(date -u +%Y%m%d%H%M%S).$$
backup=
tmp="${target}.distronexus.tmp.$$"
cleanup() { rm -f "$tmp"; }
trap cleanup EXIT HUP INT TERM
if [ -e "$target" ]; then backup="${target}.distronexus.${timestamp}.bak"; cp -p -- "$target" "$backup"; fi
install -o root -g root -m 0644 -- "$source_file" "$tmp"
sync "$tmp"
mv -f -- "$tmp" "$target"
trap - EXIT HUP INT TERM
printf '%s\n' "$backup"
""";

    private const string ReadHelper = """
#!/bin/sh
set -eu
target=/etc/wsl.conf
if [ ! -e "$target" ]; then printf 'DNX_ABSENT\n'; exit 0; fi
printf 'DNX_DATA\n'
base64 "$target"
""";
}
