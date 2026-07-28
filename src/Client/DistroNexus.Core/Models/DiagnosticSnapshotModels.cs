namespace DistroNexus.Core.Models;

/// <summary>Bounded, presentation-safe diagnostic state returned by the module boundary.</summary>
public sealed record DiagnosticSnapshotResult(
    string ModuleState,
    string WslState,
    string BridgeState,
    IReadOnlyList<DiagnosticNotice> Notices,
    string OutcomeCode);

/// <summary>A display-safe diagnostic notice; it intentionally contains no host diagnostics.</summary>
public sealed record DiagnosticNotice(string Code, string Severity, string Message);
