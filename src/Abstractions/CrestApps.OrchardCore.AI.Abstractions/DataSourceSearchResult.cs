namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Represents a search result from a data source embedding index.
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
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the similarity score.
    /// </summary>
    public float Score { get; set; }
}
