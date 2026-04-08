using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.Infrastructure.Indexing.DataSources;

/// <summary>
/// Manages document chunks within a data source index, supporting similarity search
/// and bulk deletion. Implementations are registered as keyed services using the
/// index provider name as the key.
/// </summary>
public interface IDataSourceContentManager
{
    /// <summary>
    /// Searches for document chunks that are similar to the provided embedding vector.
    /// </summary>
    /// <param name="indexProfile">The index profile describing the target index.</param>
    /// <param name="embedding">The embedding vector to search against.</param>
    /// <param name="dataSourceId">The identifier of the data source to search within.</param>
    /// <param name="topN">The maximum number of results to return.</param>
    /// <param name="filter">An optional filter expression to narrow the search.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An enumerable of search results ranked by similarity.</returns>
    Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all document chunks belonging to the specified data source from the index.
    /// </summary>
    /// <param name="indexProfile">The index profile describing the target index.</param>
    /// <param name="dataSourceId">The identifier of the data source whose chunks should be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of document chunks deleted.</returns>
    Task<long> DeleteByDataSourceIdAsync(
        IIndexProfileInfo indexProfile,
        string dataSourceId,
        CancellationToken cancellationToken = default);
}
