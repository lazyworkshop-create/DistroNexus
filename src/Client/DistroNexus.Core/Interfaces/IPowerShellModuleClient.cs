namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides the closed set of DistroNexus module operations available to presentation clients.
/// </summary>
public interface IPowerShellModuleClient
{
    /// <summary>
    /// Gets tags for every instance, or for one instance when <paramref name="name"/> is supplied.
    /// </summary>
    Task<IReadOnlyList<DistroNexusInstanceTagResult>> GetInstanceTagsAsync(
        string? name = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the stable module result for an instance tag query.
/// </summary>
public sealed record DistroNexusInstanceTagResult(string Name, IReadOnlyList<string> Tags);
