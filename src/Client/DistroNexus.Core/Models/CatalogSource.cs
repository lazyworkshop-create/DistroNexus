namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a catalog source for distribution packages.
/// </summary>
public class CatalogSource
{
    /// <summary>
    /// Gets or sets the unique identifier for the source.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the source.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the catalog source.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the source.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this source is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this is a default source.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets the priority/order of this source.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Gets or sets the date when this source was last accessed.
    /// </summary>
    public DateTime? LastAccessed { get; set; }

    /// <summary>
    /// Gets or sets the date when this source was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional metadata for the source.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}