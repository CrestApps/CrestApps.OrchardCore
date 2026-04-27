namespace CrestApps.OrchardCore.AI.Memory.Models;

/// <summary>
/// Represents the AI memory entry index document.
/// </summary>
public sealed class AIMemoryEntryIndexDocument
{
    /// <summary>
    /// Gets or sets the memory id.
    /// </summary>
    public string MemoryId { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the updated utc.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the embedding.
    /// </summary>
    public float[] Embedding { get; set; }
}
