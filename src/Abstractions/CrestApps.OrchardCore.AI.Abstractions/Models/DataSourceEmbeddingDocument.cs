namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a document stored in the data source embedding master index.
/// Each document maps to a source record and contains its embedding chunks.
/// </summary>
public sealed class DataSourceEmbeddingDocument
{
    /// <summary>
    /// Gets or sets the reference ID (key) of the source document.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the data source ID this document belongs to.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the title of the source document.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the full text content of the source document.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the source document.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the text chunks with their embeddings.
    /// </summary>
    public List<DataSourceEmbeddingChunk> Chunks { get; set; }
}
