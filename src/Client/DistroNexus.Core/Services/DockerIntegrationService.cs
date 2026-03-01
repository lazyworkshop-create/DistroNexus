using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
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

    public DockerIntegrationService(ILogger<DockerIntegrationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
