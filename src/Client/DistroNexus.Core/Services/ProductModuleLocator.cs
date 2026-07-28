namespace DistroNexus.Core.Services;

/// <summary>Resolves only product-owned module locations selected at build composition time.</summary>
public sealed class ProductModuleLocator
{
    private readonly string _baseDirectory;
    private readonly bool _developmentBuild;
    private readonly string? _developmentDirectory;
    public ProductModuleLocator() : this(AppContext.BaseDirectory, true) { }
    internal ProductModuleLocator(string baseDirectory, bool developmentBuild) { _baseDirectory = baseDirectory; _developmentBuild = developmentBuild; }
    internal ProductModuleLocator(string baseDirectory, bool developmentBuild, string developmentDirectory) : this(baseDirectory, developmentBuild) { _developmentDirectory = developmentDirectory; }
    public string? Resolve()
    {
        var packaged = Path.Combine(_baseDirectory, "PowerShell");
        if (HasModule(packaged)) return packaged;

#if DEBUG
        if (_developmentBuild)
        {
        // Development builds may use the repository module, never a user supplied location.
        var development = _developmentDirectory ?? Path.GetFullPath(Path.Combine(_baseDirectory, "..", "..", "..", "..", "..", "PowerShell"));
        if (HasModule(development)) return development;
        }
#endif
        return null;
    }

    private static bool HasModule(string directory) =>
        File.Exists(Path.Combine(directory, "DistroNexus.psd1")) &&
        File.Exists(Path.Combine(directory, "DistroNexus.psm1"));
}
