using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI;

public interface IDataSourceContentManager
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

    /// <summary>
    /// Deletes all document chunks belonging to the specified data source from the index.
    /// </summary>
    /// <param name="indexProfile">The index profile to delete from.</param>
    /// <param name="dataSourceId">The data source ID whose documents should be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of documents deleted.</returns>
    Task<long> DeleteByDataSourceIdAsync(
        IndexProfile indexProfile,
        string dataSourceId,
        CancellationToken cancellationToken = default);
}
