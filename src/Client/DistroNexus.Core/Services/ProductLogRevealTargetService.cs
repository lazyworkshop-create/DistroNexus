using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Resolves and creates the product log directory without disclosing host paths to Desktop.</summary>
public sealed class ProductLogRevealTargetService
{
    private readonly SettingsService _settings;
    private readonly Func<string>? _configuredPath;
    public ProductLogRevealTargetService(SettingsService settings) => _settings = settings;
    public ProductLogRevealTargetService(Func<string> configuredPath) => _configuredPath = configuredPath ?? throw new ArgumentNullException(nameof(configuredPath));

    public ProductLogRevealTarget GetRevealTarget()
    {
        try
        {
            var configured = _configuredPath?.Invoke() ?? _settings.LoadSettings().LogPath;
            var candidate = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus", "Logs")
                : configured;
            if (!Path.IsPathFullyQualified(candidate) || candidate.StartsWith("\\\\", StringComparison.Ordinal) || candidate.StartsWith("\\\\?\\", StringComparison.Ordinal))
                return new(null, "ProductLog.Unavailable");
            var full = Path.GetFullPath(candidate);
            if (Path.GetPathRoot(full)?.Equals(full, StringComparison.OrdinalIgnoreCase) == true || HasReparsePoint(full)) return new(null, "ProductLog.Unavailable");
            Directory.CreateDirectory(full);
            if (HasReparsePoint(full)) return new(null, "ProductLog.Unavailable");
            return new(new Uri(full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar), "ProductLog.Ready");
        }
        catch { return new(null, "ProductLog.Unavailable"); }
    }
    private static bool HasReparsePoint(string path)
    {
        for (var current = path; ; current = Directory.GetParent(current)?.FullName ?? string.Empty)
        {
            if (string.IsNullOrEmpty(current)) return false;
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) return false;
        }
    }
}
