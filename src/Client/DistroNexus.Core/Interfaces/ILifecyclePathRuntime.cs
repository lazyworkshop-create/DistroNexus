using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Fixed, Core-owned runtime operations used only after lifecycle grant validation.</summary>
public interface ILifecyclePathRuntime
{
    Task<List<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default);
    Task RemoveAsync(string instanceName, bool keepFiles, CancellationToken cancellationToken = default);
    Task MoveAsync(string instanceName, string targetDirectory, CancellationToken cancellationToken = default);
    Task RenameAsync(string instanceName, string newName, CancellationToken cancellationToken = default);
    Task ExportAsync(string instanceName, string destination, bool stopRunning, CancellationToken cancellationToken = default);
    Task ImportAsync(string instanceName, string source, string targetDirectory, CancellationToken cancellationToken = default);
}
