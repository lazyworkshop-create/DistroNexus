using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DistroNexus.Core.Services;

/// <summary>
/// Reads and writes Docker Desktop WSL 2 integration settings.
/// Supports Docker Desktop 4.30+ (settings-store.json) with fallback to settings.json.
/// </summary>
public class DockerIntegrationService : IDockerIntegrationService
{
    private readonly ILogger<DockerIntegrationService> _logger;
    private readonly IWslManagerService _wslManager;

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

    public DockerIntegrationService(ILogger<DockerIntegrationService> logger, IWslManagerService wslManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
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
            throw new ArgumentException("Instance name cannot be empty.", nameof(instanceName));

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

    /// <inheritdoc/>
    public async Task SetIntegrationAsync(
        string instanceName,
        bool enabled,
        CancellationToken ct = default)
    {
        if (instanceName is null) throw new ArgumentNullException(nameof(instanceName));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Instance name cannot be empty.", nameof(instanceName));

        // Reserved distros must never be toggled — matches GetIntegrationStatusAsync contract
        if (ReservedDistros.Contains(instanceName))
            throw new ArgumentException(
                $"Cannot modify Docker integration for reserved distro '{instanceName}'.",
                nameof(instanceName));

        // Check WSL version — Docker Desktop integration requires WSL v2
        var instance = await GetInstanceAsync(instanceName, ct);
        // Fail-open: if version cannot be determined, allow the operation to proceed and let file I/O surface the real error.
        if (instance?.Version == 1)
            throw new WslOperationFailedException(
                $"Docker Desktop integration requires WSL v2. Instance '{instanceName}' is WSL v1.",
                DistroNexusErrorCode.WslVersionTooLow,
                operation: "SetDockerIntegration",
                instanceName: instanceName);

        var settingsPath = ResolveSettingsPath();
        if (settingsPath is null)
            throw new InvalidOperationException("Docker Desktop settings file not found.");

        // Read current content (or empty object if missing)
        JsonObject root;
        if (File.Exists(settingsPath))
        {
            var json = await File.ReadAllTextAsync(settingsPath, ct);
            root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        // Build updated list
        var current = root["integratedWslDistros"]?.AsArray()
            ?? new JsonArray();

        var names = current
            .Select(d => d?.GetValue<string>())
            .Where(n => n is not null)
            .ToList()!;

        if (enabled)
        {
            if (!names.Contains(instanceName, StringComparer.OrdinalIgnoreCase))
                names.Add(instanceName);
        }
        else
        {
            names.RemoveAll(n =>
                string.Equals(n, instanceName, StringComparison.OrdinalIgnoreCase));
        }

        var newArray = new JsonArray(names.Select(n => JsonValue.Create(n)).ToArray<JsonNode?>());
        root["integratedWslDistros"] = newArray;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var updated = root.ToJsonString(options);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(settingsPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(settingsPath, updated, ct);
        _logger.LogInformation(
            "Docker integration {Action} for {Instance}",
            enabled ? "enabled" : "disabled",
            instanceName);
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

    private static string? ResolveSettingsPath()
    {
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
}
