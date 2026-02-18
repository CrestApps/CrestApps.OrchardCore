namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a single chunk of an AI document for indexing purposes.
/// This model is used as the record in <see cref="OrchardCore.Indexing.BuildDocumentIndexContext"/>
/// when indexing document chunks via <see cref="OrchardCore.Indexing.IDocumentIndexHandler"/>.
/// </summary>
public sealed class AIDocumentChunk
{
    /// <summary>
    /// Gets or sets the unique identifier for this chunk (format: "{documentId}_{chunkIndex}").
    /// </summary>
    public string ChunkId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent document.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the original file name of the parent document.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owning resource (e.g., AI Profile ID or Chat Interaction ID).
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the type of the owning resource (e.g., "profile" or "chatinteraction").
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the chunk index within the parent document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector for this chunk.
    /// </summary>
    public float[] Embedding { get; set; }
}
