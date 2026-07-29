namespace DistroNexus.Core.Models;

public enum ConfigurationTokenKind { Blank, Comment, Section, KeyValue, Malformed }
public enum ConfigurationDiagnosticSeverity { Warning, Error }
public enum ConfigurationValueType { String, Boolean, Integer, MemorySize, Enum, Path, PortList }

public sealed record ConfigurationDiagnostic(int Line, string Code, string Message,
    ConfigurationDiagnosticSeverity Severity = ConfigurationDiagnosticSeverity.Error);

public sealed record ConfigurationToken(
    ConfigurationTokenKind Kind,
    int Line,
    string Raw,
    string LineEnding,
    string? Section = null,
    string? Key = null,
    string? Value = null,
    int ValueStart = -1,
    int ValueLength = 0);

public sealed record WslSettingDefinition(
    string Section,
    string Key,
    ConfigurationValueType Type,
    RestartScope RestartScope,
    IReadOnlySet<string>? AllowedValues = null,
    int? Minimum = null,
    int? Maximum = null,
    string? RequiredCapability = null,
    bool Experimental = false);

public sealed record WslConfigurationSettings(IReadOnlyDictionary<string, string> Values);
public sealed record DistributionConfigurationSettings(IReadOnlyDictionary<string, string> Values);

public sealed record ConfigurationDocument<TSettings>(
    TSettings Settings,
    LosslessIniDocument Source,
    IReadOnlyList<ConfigurationDiagnostic> Diagnostics,
    int UnknownKeyCount,
    string Fingerprint,
    RestartScope RestartScope,
    string RawPreview);

public sealed record ConfigurationSaveResult(string Fingerprint, string? BackupPath, RestartScope RestartScope);
public sealed record ConfigurationPreview(string CurrentRaw, string DesiredRaw, IReadOnlyList<string> ChangedSettings, RestartScope RestartScope);

public sealed class ConfigurationConflictException(string message) : IOException(message);
public sealed class ConfigurationTransportException(string code, string message) : IOException(message)
{
    public string Code { get; } = code;
}
public sealed class ConfigurationValidationException(IReadOnlyList<ConfigurationDiagnostic> diagnostics)
    : ArgumentException("Configuration contains invalid or unsupported values.")
{
    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; } = diagnostics;
}
