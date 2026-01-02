namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Represents a search result from the document index.
/// </summary>
public sealed class DocumentChunkSearchResult
{
    /// <summary>
    /// Gets or sets the document chunk.
    /// </summary>
    public Models.DocumentChunk Chunk { get; set; }

    /// <summary>
    /// Gets or sets the similarity score.
    /// </summary>
    public float Score { get; set; }
}
