using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.Infrastructure.Indexing;

/// <summary>
/// Manages the lifecycle of search indexes in a search backend (e.g., Elasticsearch, Azure AI Search).
/// Implementations are registered as keyed services using the provider name as the key.
/// </summary>
public interface ISearchIndexManager
{
    /// <summary>
    /// Checks whether the specified index exists.
    /// </summary>
    /// <param name="profile">The index profile.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the index exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Composes the full remote index name for the supplied profile.
    /// </summary>
    /// <param name="profile">The index profile.</param>
    /// <returns>The fully qualified index name.</returns>
    string ComposeIndexFullName(IIndexProfileInfo profile);

    /// <summary>
    /// Creates a new search index with the specified fields.
    /// </summary>
    /// <param name="profile">The index profile describing the index.</param>
    /// <param name="fields">The field definitions for the index schema.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task CreateAsync(IIndexProfileInfo profile, IReadOnlyCollection<SearchIndexField> fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified search index.
    /// </summary>
    /// <param name="profile">The index profile.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default);
}
