namespace DistroNexus.Core.Models;

/// <summary>Public, path-free representation of the supported global WSL configuration.</summary>
public sealed record GlobalConfigurationSnapshot(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> SupportedFields,
    IReadOnlyList<string> Capabilities,
    string DisplayPreview,
    bool PendingRestart,
    long HostRamMb,
    int HostCpuCount);

public sealed record GlobalConfigurationPreview(
    IReadOnlyDictionary<string, string?> Changes,
    IReadOnlyList<string> ChangedSettings,
    string DisplayPreview,
    bool PendingRestart,
    string PreviewToken);

public sealed record GlobalConfigurationApplyResult(IReadOnlyList<string> ChangedSettings, bool PendingRestart);
