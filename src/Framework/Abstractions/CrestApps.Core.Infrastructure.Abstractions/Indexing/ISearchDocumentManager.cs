using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.Infrastructure.Indexing;

/// <summary>
/// Manages documents within a search index (add, update, delete).
/// Implementations are registered as keyed services using the provider name as the key.
/// </summary>
public interface ISearchDocumentManager
{
    /// <summary>
    /// Adds or updates documents in the specified index.
    /// </summary>
    /// <param name="profile">The index profile describing the target index.</param>
    /// <param name="documents">The documents to add or update.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    Task<bool> AddOrUpdateAsync(IIndexProfileInfo profile, IReadOnlyCollection<IndexDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes specific documents from the index by their IDs.
    /// </summary>
    /// <param name="profile">The index profile describing the target index.</param>
    /// <param name="documentIds">The IDs of the documents to delete.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAsync(IIndexProfileInfo profile, IEnumerable<string> documentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all documents from the specified index.
    /// </summary>
    /// <param name="profile">The index profile describing the target index.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAllAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default);
}
