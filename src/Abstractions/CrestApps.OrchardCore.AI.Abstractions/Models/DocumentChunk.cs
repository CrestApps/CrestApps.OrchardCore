namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a document chunk for embedding and indexing.
/// Each chunk contains a paragraph or section of the original document.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector for this chunk.
    /// </summary>
    public float[] Embedding { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the document.
    /// </summary>
    public int Index { get; set; }
}
