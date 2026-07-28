using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace DistroNexus.Core.Services;

/// <summary>
/// Reads and writes Docker Desktop WSL 2 integration settings.
/// Supports Docker Desktop 4.30+ (settings-store.json) with fallback to settings.json.
/// </summary>
public class DockerIntegrationService : IDockerIntegrationService
{
    private readonly ILogger<DockerIntegrationService> _logger;
    private readonly IWslManagerService _wslManager;
    private readonly DockerIntegrationGrantStore _grants;
    private readonly string? _settingsPathForTests;

    // Reserved distro names that must never be toggled
    private static readonly HashSet<string> ReservedDistros = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker-desktop",
        "docker-desktop-data"
    };

    // Docker Desktop executable path
    private static string DockerExePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Docker", "Docker Desktop.exe");

    public DockerIntegrationService(ILogger<DockerIntegrationService> logger, IWslManagerService wslManager, string? grantRoot = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _grants = new DockerIntegrationGrantStore(grantRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus"));
    }
    internal DockerIntegrationService(ILogger<DockerIntegrationService> logger, IWslManagerService wslManager, string grantRoot, string settingsPathForTests) : this(logger, wslManager, grantRoot) => _settingsPathForTests = settingsPathForTests;

    public async Task<DockerIntegrationSnapshot> GetSnapshotAsync(string instanceName, CancellationToken ct = default)
    {
        var eligibility = await ValidateEligibilityAsync(instanceName, ct);
        if (eligibility is not null) return new(false, false, "Unavailable", eligibility, null, null);
        var path = ResolveExistingSettingsPath();
        if (path is null) return new(true, false, "Unavailable", "Docker settings are unavailable.", await GetDockerDesktopVersionAsync(ct), null);
        try
        {
            var root = ParseSettings(await File.ReadAllTextAsync(path, ct));
            var names = ReadDistroNames(root);
            return new(true, true, names.Contains(instanceName, StringComparer.OrdinalIgnoreCase) ? "Enabled" : "Disabled", null, await GetDockerDesktopVersionAsync(ct), "Restart Docker Desktop to apply this change.");
        }
        catch { return new(true, false, "Unavailable", "Docker settings are invalid.", await GetDockerDesktopVersionAsync(ct), null); }
    }

    public async Task<DockerIntegrationPreview> PreviewSetAsync(string instanceName, bool enabled, CancellationToken ct = default)
    {
        var eligibility = await ValidateEligibilityAsync(instanceName, ct);
        if (eligibility is not null) throw new InvalidOperationException(eligibility);
        var path = ResolveExistingSettingsPath() ?? throw new InvalidOperationException("Docker settings are unavailable.");
        var content = await File.ReadAllTextAsync(path, ct);
        var root = ParseSettings(content);
        _ = ReadDistroNames(root);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new DockerIntegrationGrant(instanceName, enabled, Fingerprint(content), FileIdentity(path), DateTimeOffset.UtcNow.AddMinutes(2));
        await _grants.IssueAsync(token, record, ct);
        return new DockerIntegrationPreview(token, enabled, record.ExpiresAt, [enabled ? "Enable Docker Desktop WSL integration." : "Disable Docker Desktop WSL integration."], []);
    }

    public async Task<DockerIntegrationResult> SetFromPreviewAsync(string previewToken, string instanceName, bool enabled, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken) || previewToken.Length != 64 || previewToken.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("DockerIntegration.PreviewInvalid");
        DockerIntegrationGrant grant = await _grants.ConsumeAsync(previewToken, ct);
        try
        {
            if (grant.ExpiresAt < DateTimeOffset.UtcNow) throw new InvalidOperationException("DockerIntegration.PreviewExpired");
            if (!string.Equals(grant.Name, instanceName, StringComparison.Ordinal) || grant.Enabled != enabled) throw new InvalidOperationException("DockerIntegration.PreviewMismatch");
            var eligibility = await ValidateEligibilityAsync(instanceName, ct);
            if (eligibility is not null) throw new InvalidOperationException("DockerIntegration.PreviewStale");
            var path = ResolveExistingSettingsPath() ?? throw new InvalidOperationException("DockerIntegration.PreviewStale");
            await using var writeGate = await OpenSettingsLockAsync(path, ct);
            await using var selected = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true);
            var original = await new StreamReader(selected, leaveOpen: true).ReadToEndAsync(ct);
            if (grant.Identity != FileIdentity(selected) || grant.Fingerprint != Fingerprint(original)) throw new InvalidOperationException("DockerIntegration.PreviewStale");
            var root = ParseSettings(original);
            var names = ReadDistroNames(root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (enabled && !names.Contains(instanceName, StringComparer.OrdinalIgnoreCase)) names.Add(instanceName);
            if (!enabled) names.RemoveAll(x => string.Equals(x, instanceName, StringComparison.OrdinalIgnoreCase));
            root["integratedWslDistros"] = new JsonArray(names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
            selected.Close();
            await AtomicWriteAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
            return new(true, "DockerIntegration.Updated", true, "Restart Docker Desktop to apply this change.");
        }
        finally { }
    }

    /// <inheritdoc/>
    public virtual Task<bool> IsDockerDesktopInstalledAsync(CancellationToken ct = default)
    {
        // Check local app data path first (primary)
        if (File.Exists(DockerExePath))
            return Task.FromResult(true);

        // Registry fallback
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Docker Inc.\Docker Desktop");
            return Task.FromResult(key != null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Registry check for Docker Desktop failed");
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public virtual Task<string?> GetDockerDesktopVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to read version from Docker Desktop exe file info
            if (!File.Exists(DockerExePath)) return Task.FromResult<string?>(null);
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(DockerExePath);
            return Task.FromResult<string?>(version.FileVersion);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read Docker Desktop version from {Path}", DockerExePath);
            return Task.FromResult<string?>(null);
        }
    }

    /// <inheritdoc/>
    public async Task<DockerIntegrationStatus> GetIntegrationStatusAsync(
        string instanceName,
        CancellationToken ct = default)
    {
        if (instanceName is null) throw new ArgumentNullException(nameof(instanceName));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new WslOperationFailedException(
                "Instance name cannot be empty.",
                DistroNexusErrorCode.InstanceNotFound,
                operation: "GetDockerIntegrationStatus");

        // Reserved distros are never eligible
        if (ReservedDistros.Contains(instanceName))
            return DockerIntegrationStatus.Unavailable;

        if (!await IsDockerDesktopInstalledAsync(ct))
            return DockerIntegrationStatus.Unavailable;

        var settingsPath = ResolveSettingsPath();
        if (settingsPath is null || !File.Exists(settingsPath))
            return DockerIntegrationStatus.Unavailable;

        try
        {
            var json = await File.ReadAllTextAsync(settingsPath, ct);
            var node = JsonNode.Parse(json);
            var distros = node?["integratedWslDistros"]?.AsArray();

            if (distros is null)
                return DockerIntegrationStatus.Disabled;

            var names = distros
                .Select(d => d?.GetValue<string>())
                .Where(n => n is not null)
                .ToList();

            return names.Contains(instanceName, StringComparer.OrdinalIgnoreCase)
                ? DockerIntegrationStatus.Enabled
                : DockerIntegrationStatus.Disabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Docker settings at {Path}", settingsPath);
            return DockerIntegrationStatus.Unavailable;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets a single WSL instance by name.
    /// </summary>
    /// <param name="instanceName">The name of the instance to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The instance, or null if not found.</returns>
    private async Task<WslInstance?> GetInstanceAsync(string instanceName, CancellationToken ct)
    {
        try
        {
            var instances = await _wslManager.GetInstancesAsync(ct);
            return instances.FirstOrDefault(i => string.Equals(i.Name, instanceName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to retrieve WSL instance {InstanceName}", instanceName);
            return null;
        }
    }

    private string? ResolveSettingsPath()
    {
        if (_settingsPathForTests is not null) return _settingsPathForTests;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dockerDir = Path.Combine(appData, "Docker");

        // Docker 4.30+ primary settings file
        var newPath = Path.Combine(dockerDir, "settings-store.json");
        if (File.Exists(newPath)) return newPath;

        // Legacy fallback
        var legacyPath = Path.Combine(dockerDir, "settings.json");
        if (File.Exists(legacyPath)) return legacyPath;

        // Return the preferred path even if missing (writer will create it)
        return newPath;
    }

    private async Task<string?> ValidateEligibilityAsync(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\r','\n','\0']) >= 0) return "Instance name is invalid.";
        if (ReservedDistros.Contains(name)) return "The instance is reserved.";
        if (!await IsDockerDesktopInstalledAsync(ct)) return "Docker Desktop is unavailable.";
        var instance = await GetInstanceAsync(name, ct);
        return instance is null ? "The WSL instance is unknown." : instance.Version != 2 ? "Docker integration requires WSL2." : null;
    }
    private string? ResolveExistingSettingsPath() { var p = ResolveSettingsPath(); return p is not null && File.Exists(p) ? p : null; }
    private static JsonObject ParseSettings(string json) => JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Docker settings are invalid.");
    private static List<string> ReadDistroNames(JsonObject root)
    {
        if (root["integratedWslDistros"] is null) return [];
        if (root["integratedWslDistros"] is not JsonArray array) throw new InvalidOperationException("Docker settings are invalid.");
        var values = new List<string>();
        foreach (var item in array) { if (item is not JsonValue valueNode || !valueNode.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Docker settings are invalid."); values.Add(value); }
        return values;
    }
    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private static string FileIdentity(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return FileIdentity(stream); }
    private static string FileIdentity(FileStream stream)
    {
        if (!OperatingSystem.IsWindows()) { var info = new FileInfo(stream.Name); return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}"; }
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var fileInfo)) throw new IOException("DockerIntegration.PreviewStale");
        return $"{fileInfo.VolumeSerialNumber:X8}:{fileInfo.FileIndexHigh:X8}{fileInfo.FileIndexLow:X8}";
    }
    private static async Task AtomicWriteAsync(string path, string content, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            { var bytes = System.Text.Encoding.UTF8.GetBytes(content); await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct); stream.Flush(true); }
            File.Replace(temp, path, path + ".distronexus.bak", true);
            return;
        }
        finally { if (File.Exists(temp)) try { File.Delete(temp); } catch (IOException) { } }
    }
    private static async Task<FileStream> OpenSettingsLockAsync(string path, CancellationToken ct)
    {
        var lockPath = path + ".distronexus.lock";
        for (var attempt = 0; ; attempt++) try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) when (attempt < 20) { await Task.Delay(50, ct); }
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes; public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime; public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime; public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber; public uint FileSizeHigh; public uint FileSizeLow; public uint NumberOfLinks; public uint FileIndexHigh; public uint FileIndexLow;
    }
}
