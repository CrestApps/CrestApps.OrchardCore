namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a document chunk for embedding and indexing.
/// Each chunk contains a paragraph or section of the original document.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Gets or sets the unique identifier for this chunk.
    /// </summary>
    public string ChunkId { get; set; }

    /// <summary>
    /// Gets or sets the parent document identifier.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the session/interaction identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector for this chunk.
    /// </summary>
    public float[] Embedding { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the original file name of the source document.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the chunk was indexed.
    /// </summary>
    public DateTime IndexedUtc { get; set; }
}
