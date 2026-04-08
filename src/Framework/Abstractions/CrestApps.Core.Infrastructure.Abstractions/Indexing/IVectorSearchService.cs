using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.Infrastructure.Indexing;

/// <summary>
/// Interface for searching document embeddings in various index providers.
/// Implementations should be registered as keyed services using the provider name.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Searches for document chunks that are similar to the provided embedding vector.
    /// </summary>
    /// <param name="indexProfile">The index profile describing the target index.</param>
    /// <param name="embedding">The embedding vector to search against.</param>
    /// <param name="referenceId">The reference entity identifier to scope the search.</param>
    /// <param name="referenceType">The type of the reference entity.</param>
    /// <param name="topN">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An enumerable of document chunk search results ranked by similarity.</returns>
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string referenceId,
        string referenceType,
        int topN,
        CancellationToken cancellationToken = default);
}
