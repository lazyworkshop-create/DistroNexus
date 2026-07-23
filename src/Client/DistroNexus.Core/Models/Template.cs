using CommunityToolkit.Mvvm.ComponentModel;

namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a template for setting up a development environment in a WSL instance.
/// </summary>
public partial class Template : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = "Apps24";
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> CompatibleDistros { get; set; } = new();
    public int EstimatedDurationMinutes { get; set; }
    public long EstimatedDiskSpaceMB { get; set; }

    public List<TemplateScript> Scripts { get; set; } = new();
    public List<TemplatePackage> Packages { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public Dictionary<string, string> DefaultSelections { get; set; } = new();
    public List<TemplateVersionOption> VersionOptions { get; set; } = new();
    public List<TemplatePreflightCheck> PreflightChecks { get; set; } = new();
    public List<TemplateOutputArtifact> OutputArtifacts { get; set; } = new();
    public List<string> ScenarioTags { get; set; } = new();
    public TemplateInstallMode InstallMode { get; set; } = TemplateInstallMode.Scripted;
    public bool IsOfficial { get; set; }
    public bool IsCustom { get; set; }
    /// <summary>Marketplace provenance shown to users; legacy templates remain local/built-in.</summary>
    public string SourceUrl { get; set; } = string.Empty;
    public string PublisherFingerprint { get; set; } = string.Empty;
    public TemplateTrustState TrustState { get; set; } = TemplateTrustState.Untrusted;
    public List<TemplateCapability> Capabilities { get; set; } = new();
    /// <summary>Remote marketplace v2 templates are deliberately non-executable until materialized locally by a reviewed workflow.</summary>
    public bool IsRemoteV2 { get; set; }
    public string ArtifactSha256 { get; set; } = string.Empty;
    /// <summary>Canonical marketplace manifest identity bound to the materialized artifact.</summary>
    public string MarketplaceManifestDigest { get; set; } = string.Empty;
    /// <summary>Internal immutable marketplace artifact root. It is never accepted from user template files.</summary>
    public string MarketplaceArtifactRoot { get; set; } = string.Empty;
    public List<TemplateExecutableFile> MarketplaceExecutableFiles { get; set; } = new();

    [ObservableProperty]
    private bool _isInstalled;
}

public class TemplateVersionOption
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TemplateOptionType Type { get; set; } = TemplateOptionType.Select;
    public bool Required { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public List<TemplateOptionValue> Options { get; set; } = new();
}

public enum TemplateOptionType
{
    Select,
    MultiSelect,
    Text
}

public class TemplateOptionValue
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class TemplatePreflightCheck
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public TemplateScriptType Type { get; set; } = TemplateScriptType.Bash;
    public bool Required { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
    public string AppliesToVariable { get; set; } = string.Empty;
    public string AppliesToValue { get; set; } = string.Empty;
}

public class TemplateOutputArtifact
{
    public string Type { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public enum TemplateInstallMode
{
    PackageManager,
    VersionManager,
    Scripted
}

/// <summary>
/// Represents a script to be executed as part of a template.
/// </summary>
public class TemplateScript
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TemplateScriptPhase Phase { get; set; }
    public TemplateScriptType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool ContinueOnError { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// Represents the execution phase of a template script.
/// </summary>
public enum TemplateScriptPhase
{
    PreImport,      // Executed on host before wsl --import
    PostImport,     // Executed in WSL after import, before user configuration
    PostConfigure,  // Executed in WSL after user configuration
    FirstBoot       // Executed on first instance startup
}

/// <summary>
/// Represents the type of a template script.
/// </summary>
public enum TemplateScriptType
{
    Bash,           // Bash script (inside WSL)
    PowerShell,     // PowerShell script (on host)
    Python,         // Python script
    Inline          // Inline command
}

/// <summary>
/// Represents a package to be installed as part of a template.
/// </summary>
public class TemplatePackage
{
    public string Name { get; set; } = string.Empty;
    public string PackageManager { get; set; } = "apt";
    public string Version { get; set; } = string.Empty;
    public bool Essential { get; set; } = true;
}

/// <summary>
/// Represents the result of applying a template.
/// </summary>
public class TemplateApplicationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ExecutedScripts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string LogFilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    /// <summary>Safe pre-execution guidance; this never creates a recovery point automatically.</summary>
    public string? RecoveryRecommendation { get; set; }
}

/// <summary>
/// Represents the progress of applying a template.
/// </summary>
public class TemplateProgress
{
    public string CurrentPhase { get; set; } = string.Empty;
    public string CurrentScript { get; set; } = string.Empty;
    public int TotalScripts { get; set; }
    public int CompletedScripts { get; set; }
    public double PercentComplete { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string LatestOutput { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of validating a template.
/// </summary>
public class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
