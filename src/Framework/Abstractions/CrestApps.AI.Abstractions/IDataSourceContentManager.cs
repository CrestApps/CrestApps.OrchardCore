namespace CrestApps.AI;

public interface IDataSourceContentManager
{
    /// <summary>
    /// Searches for document chunks that are similar to the provided embedding vector.
    /// </summary>
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
    Task<long> DeleteByDataSourceIdAsync(
        IIndexProfileInfo indexProfile,
        string dataSourceId,
        CancellationToken cancellationToken = default);
}
