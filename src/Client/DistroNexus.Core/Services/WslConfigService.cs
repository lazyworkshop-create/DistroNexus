using System.Runtime.InteropServices;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Line-based INI reader/writer for ~/.wslconfig.
/// </summary>
public class WslConfigService : IWslConfigService
{
    private readonly ILogger<WslConfigService> _logger;
    private readonly string _userProfileDir;

    public WslConfigService(ILogger<WslConfigService> logger, string? userProfileDir = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userProfileDir = userProfileDir
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private string WslConfigPath => Path.Combine(_userProfileDir, ".wslconfig");

    // ── IWslConfigService ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<WslConfig> GetWslConfigAsync(CancellationToken ct = default)
    {
        var config = new WslConfig();

        if (!File.Exists(WslConfigPath))
            return config;

        var lines = await File.ReadAllLinesAsync(WslConfigPath, ct);
        bool inWsl2Section = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.StartsWith('['))
            {
                inWsl2Section = line.Equals("[wsl2]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inWsl2Section) continue;
            if (line.StartsWith('#') || line.StartsWith(';') || !line.Contains('=')) continue;

            var idx = line.IndexOf('=');
            var key = line[..idx].Trim().ToLowerInvariant();
            var val = line[(idx + 1)..].Trim();

            switch (key)
            {
                case "memory":             config.Memory = val; break;
                case "processors":
                    if (int.TryParse(val, out var p)) config.Processors = p;
                    break;
                case "swap":               config.Swap = val; break;
                case "localhostforwarding":
                    if (bool.TryParse(val, out var lf)) config.LocalhostForwarding = lf;
                    break;
                case "networkingmode":     config.NetworkingMode = val; break;
            }
        }

        return config;
    }

    /// <inheritdoc/>
    public async Task SetWslConfigAsync(WslConfig config, CancellationToken ct = default)
    {
        // Read existing lines (or start fresh)
        List<string> lines = File.Exists(WslConfigPath)
            ? [.. await File.ReadAllLinesAsync(WslConfigPath, ct)]
            : [];

        // Ensure [wsl2] section exists
        int sectionIndex = lines.FindIndex(l =>
            l.Trim().Equals("[wsl2]", StringComparison.OrdinalIgnoreCase));

        if (sectionIndex < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add(string.Empty);
            lines.Add("[wsl2]");
            sectionIndex = lines.Count - 1;
        }

        // Build map of keys to update
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (config.Memory is not null) updates["memory"] = config.Memory;
        if (config.Processors.HasValue) updates["processors"] = config.Processors.Value.ToString();
        if (config.Swap is not null) updates["swap"] = config.Swap;
        if (config.LocalhostForwarding.HasValue) updates["localhostForwarding"] = config.LocalhostForwarding.Value.ToString().ToLowerInvariant();
        if (config.NetworkingMode is not null) updates["networkingMode"] = config.NetworkingMode;

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inWsl2 = false;
        int wsl2EndIndex = lines.Count; // where to insert new keys

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith('['))
            {
                if (inWsl2)
                {
                    // We've left the [wsl2] section
                    wsl2EndIndex = i;
                    break;
                }
                inWsl2 = line.Equals("[wsl2]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inWsl2) continue;
            if (line.StartsWith('#') || line.StartsWith(';') || !line.Contains('=')) continue;

            var idx = line.IndexOf('=');
            var key = line[..idx].Trim();

            if (updates.TryGetValue(key, out var newVal))
            {
                lines[i] = $"{key}={newVal}";
                applied.Add(key);
            }
        }

        // Append keys that weren't found yet
        foreach (var kv in updates)
        {
            if (!applied.Contains(kv.Key))
                lines.Insert(wsl2EndIndex++, $"{kv.Key}={kv.Value}");
        }

        await File.WriteAllLinesAsync(WslConfigPath, lines, ct);
        _logger.LogInformation("Updated .wslconfig at {Path}", WslConfigPath);
    }

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
