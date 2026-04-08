using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.Infrastructure.Indexing.DataSources;

/// <summary>
/// Reads documents from a source index. Implementations are keyed by provider name.
/// </summary>
public interface IDataSourceDocumentReader
{
    /// <summary>
    /// Reads documents from the specified source index in batches.
    /// </summary>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        IIndexProfileInfo indexProfile,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads specific documents from the source index by their native document IDs.
    /// </summary>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        IIndexProfileInfo indexProfile,
        IEnumerable<string> documentIds,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);
}
