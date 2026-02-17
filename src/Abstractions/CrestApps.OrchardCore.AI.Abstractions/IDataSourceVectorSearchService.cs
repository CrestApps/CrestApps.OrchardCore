using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Interface for searching data source embeddings in various index providers.
/// Implementations should be registered as keyed services using the provider name.
/// </summary>
public interface IDataSourceVectorSearchService
{
    /// <summary>
    /// Searches for document chunks that are similar to the provided embedding vector.
    /// </summary>
    /// <param name="indexProfile">The index profile to search.</param>
    /// <param name="embedding">The embedding vector to search for similar documents.</param>
    /// <param name="dataSourceId">The data source ID to filter results by.</param>
    /// <param name="topN">The maximum number of results to return.</param>
    /// <param name="filter">Optional provider-specific filter expression to apply before vector search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching document chunks with their similarity scores.</returns>
    Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IndexProfile indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default);
}
