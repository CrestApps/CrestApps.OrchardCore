namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Represents a search result from a data source knowledge base index.
/// </summary>
public sealed class DataSourceSearchResult
{
    /// <summary>
    /// Gets or sets the reference ID of the source document.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the title of the source document.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the text content of the matching chunk.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the reference type that identifies the kind of source
    /// (e.g., "Content" for Orchard Core content items, or the source index profile type).
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the similarity score.
    /// </summary>
    public float Score { get; set; }
}
