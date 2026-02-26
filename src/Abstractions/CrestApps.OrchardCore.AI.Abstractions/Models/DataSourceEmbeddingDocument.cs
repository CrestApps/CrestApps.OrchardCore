namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a single chunk stored in the knowledge base index.
/// Each chunk is an independent document with its own embedding and filter fields.
/// </summary>
public sealed class DataSourceEmbeddingDocument
{
    /// <summary>
    /// Gets or sets the reference ID (key) of the source document this chunk belongs to.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the data source ID this chunk belongs to.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the reference type that identifies the kind of source
    /// (e.g., "Content" for Orchard Core content items, or the source index profile type).
    /// Used to determine how links for references should be generated.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the unique chunk identifier (e.g., "{referenceId}_{chunkIndex}").
    /// </summary>
    public string ChunkId { get; set; }

    /// <summary>
    /// Gets or sets the chunk sequence index within the source document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the title of the source document.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector for this chunk.
    /// </summary>
    public float[] Embedding { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the source document.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the filter fields copied from the source document.
    /// Keys are prefixed with "filters." (e.g., "filters.status", "filters.category").
    /// </summary>
    public Dictionary<string, object> Filters { get; set; }
}
