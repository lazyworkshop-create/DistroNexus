using System.Reflection;

namespace DistroNexus.Core.Services;

/// <summary>Validates the fixed packaged operation-host assembly before a worker invokes it.</summary>
public static class WorkspaceWorkerIdentity
{
    public static void EnsureApprovedWorker(AssemblyName candidate, Version expectedVersion)
    {
        if (!string.Equals(candidate.Name, "DistroNexus.WorkspaceWorker", StringComparison.Ordinal) || candidate.Version != expectedVersion)
            throw new InvalidOperationException("Workspace.WorkerIdentityInvalid");
    }

    public static void EnsureApprovedBridge(AssemblyName candidate, Version expectedVersion)
    {
        if (!string.Equals(candidate.Name, "DistroNexus.WorkspaceBridge", StringComparison.Ordinal) || candidate.Version != expectedVersion)
            throw new InvalidOperationException("Workspace.WorkerIdentityInvalid");
    }
}
