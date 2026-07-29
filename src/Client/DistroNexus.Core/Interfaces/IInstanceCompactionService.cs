using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Closed preview/execute authority for registered-instance compaction.</summary>
public interface IInstanceCompactionService
{
    Task<InstanceCompactionPreview> PreviewAsync(string name, CancellationToken cancellationToken = default);
    Task<InstanceCompactionResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default);
}

/// <summary>Bridge-local fixed executor; paths, commands, methods and elevation never cross this boundary.</summary>
public interface IRegisteredInstanceCompactionAdapter
{
    Task<RegisteredInstanceCompactionState?> GetAsync(string name, CancellationToken cancellationToken = default);
    Task<InstanceCompactionExecution> CompactAsync(RegisteredInstanceCompactionState state, CancellationToken cancellationToken = default);
}

/// <summary>Fixed adapter outcome without a host path, command line, or process object.</summary>
public sealed record InstanceCompactionExecution(bool Succeeded, string OutcomeCode, long? AfterBytes, string Method, bool Restarted, string RecoveryAction = "None");
