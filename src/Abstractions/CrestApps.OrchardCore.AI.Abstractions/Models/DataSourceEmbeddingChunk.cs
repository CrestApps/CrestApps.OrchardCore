namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a text chunk with its embedding vector for storage in the master index.
/// </summary>
public sealed class DataSourceEmbeddingChunk
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
