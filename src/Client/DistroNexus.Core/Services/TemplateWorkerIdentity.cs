using System.Reflection;

namespace DistroNexus.Core.Services;

/// <summary>Rejects substituted worker or bridge assemblies before an operation is launched.</summary>
public static class TemplateWorkerIdentity
{
    public static void EnsureApprovedWorker(AssemblyName candidate, Version expectedVersion)
    { if (!string.Equals(candidate.Name, "DistroNexus.TemplateWorker", StringComparison.Ordinal) || candidate.Version != expectedVersion) throw new InvalidOperationException("Template.WorkerIdentityInvalid"); }
    public static void EnsureApprovedBridge(AssemblyName candidate, Version expectedVersion)
    { if (!string.Equals(candidate.Name, "DistroNexus.WorkspaceBridge", StringComparison.Ordinal) || candidate.Version != expectedVersion) throw new InvalidOperationException("Template.WorkerIdentityInvalid"); }
}
