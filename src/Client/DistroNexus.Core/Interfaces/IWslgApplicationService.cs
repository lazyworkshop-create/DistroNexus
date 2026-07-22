using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IWslgApplicationService
{
    Task<WslgApplicationStatus> GetStatusAsync(string instanceName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WslgApplication>> DiscoverAsync(string instanceName, CancellationToken cancellationToken = default);
    Task<WslgLaunchResult> LaunchAsync(WslgApplication application, CancellationToken cancellationToken = default);
    /// <summary>Reads an already parsed icon through the WSL argument-list boundary only.</summary>
    Task<byte[]?> GetIconAsync(WslgApplication application, CancellationToken cancellationToken = default);
    Task<WslgLaunchResult> RevealAsync(WslgApplication application, CancellationToken cancellationToken = default);
    Task SetPinnedAsync(string applicationId, bool pinned, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetPinsAsync(CancellationToken cancellationToken = default);
}
