namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Manages per-instance tags stored in settings.json.
/// </summary>
public interface ITagService
{
    /// <summary>Returns the tags for a given instance (empty list if none).</summary>
    Task<List<string>> GetTagsAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>Returns the union of all tags across all instances (deduplicated).</summary>
    Task<List<string>> GetAllTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces all tags for an instance. Max 10; normalised to lowercase.</summary>
    Task SetTagsAsync(string instanceName, IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>Adds a single tag to an instance (no-op if already present). Throws when at 10 tags.</summary>
    Task AddTagAsync(string instanceName, string tag, CancellationToken cancellationToken = default);

    /// <summary>Removes a single tag from an instance (no-op if not present).</summary>
    Task RemoveTagAsync(string instanceName, string tag, CancellationToken cancellationToken = default);

    /// <summary>Migrates tags from <paramref name="oldName"/> to <paramref name="newName"/> when an instance is renamed.</summary>
    Task RenameInstanceTagsAsync(string oldName, string newName, CancellationToken cancellationToken = default);

    /// <summary>Deletes all tags for an instance (e.g., when the instance is removed).</summary>
    Task DeleteInstanceTagsAsync(string instanceName, CancellationToken cancellationToken = default);
}
