namespace CrestApps.Core.Infrastructure.Indexing.Models;

/// <summary>
/// Represents a search result from the document index.
/// </summary>
public sealed class DocumentChunkSearchResult
{
    /// <summary>
    /// Gets or sets the document chunk.
    /// </summary>
    public ChatInteractionDocumentChunk Chunk { get; set; }

    /// <summary>
    /// Gets or sets the unique key that identifies the source document.
    /// </summary>
    public string DocumentKey { get; set; }

    /// <summary>
    /// Gets or sets the original file name of the source document.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the similarity score.
    /// </summary>
    public float Score { get; set; }
}
