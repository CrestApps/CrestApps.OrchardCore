using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Reads documents from a source index. Implementations are keyed by provider name.
/// </summary>
public interface IDataSourceDocumentReader
{
    /// <summary>
    /// Reads documents from the specified source index in batches.
    /// Yields key-value pairs mapping document key to a <see cref="SourceDocument"/>
    /// containing the extracted title, content, and all source fields.
    /// </summary>
    /// <param name="indexProfile">The index-profile to read from.</param>
    /// <param name="keyFieldName">The field name to use as the document key (reference ID), or null to use the native document key.</param>
    /// <param name="titleFieldName">The field name to extract the title from, or null for auto-extraction.</param>
    /// <param name="contentFieldName">The field name to extract the content from, or null to use the full document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of document key to <see cref="SourceDocument"/> pairs.</returns>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        IndexProfile indexProfile,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads specific documents from the source index by their native document IDs.
    /// Used for real-time incremental re-indexing when source documents change.
    /// </summary>
    /// <param name="indexProfile">The index-profile to read from.</param>
    /// <param name="documentIds">The native document IDs to retrieve.</param>
    /// <param name="keyFieldName">The field name to use as the document key (reference ID), or null to use the native document key.</param>
    /// <param name="titleFieldName">The field name to extract the title from, or null for auto-extraction.</param>
    /// <param name="contentFieldName">The field name to extract the content from, or null to use the full document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of document key to <see cref="SourceDocument"/> pairs.</returns>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        IndexProfile indexProfile,
        IEnumerable<string> documentIds,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);
}
