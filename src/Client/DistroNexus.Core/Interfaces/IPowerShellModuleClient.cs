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

    /// <summary>Adds a tag to an instance through the module contract.</summary>
    Task AddInstanceTagAsync(string name, string tag, CancellationToken cancellationToken = default);

    /// <summary>Replaces the tags for an instance through the module contract.</summary>
    Task SetInstanceTagsAsync(string name, IReadOnlyList<string> tags, CancellationToken cancellationToken = default);

    /// <summary>Removes a tag from an instance through the module contract.</summary>
    Task RemoveInstanceTagAsync(string name, string tag, CancellationToken cancellationToken = default);

    /// <summary>Migrates tags to an instance's new name through the module contract.</summary>
    Task RenameInstanceTagsAsync(string oldName, string newName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the stable module result for an instance tag query.
/// </summary>
public sealed record DistroNexusInstanceTagResult(string Name, IReadOnlyList<string> Tags);
