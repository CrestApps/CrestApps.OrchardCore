namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Reads documents from a source index. Implementations are keyed by provider name.
/// </summary>
public interface IDataSourceDocumentReader
{
    /// <summary>
    /// Reads documents from the specified source index in batches.
    /// Yields key-value pairs mapping document key to a <see cref="SourceDocument"/>
    /// containing the extracted title and content based on the field mapping.
    /// </summary>
    /// <param name="indexName">The name of the source index to read from.</param>
    /// <param name="titleFieldName">The field name to extract the title from, or null for auto-extraction.</param>
    /// <param name="contentFieldName">The field name to extract the content from, or null to use the full document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of document key to <see cref="SourceDocument"/> pairs.</returns>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        string indexName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document read from a source index with extracted title and content.
/// </summary>
public sealed class SourceDocument
{
    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the document content text.
    /// </summary>
    public string Content { get; set; }
}
