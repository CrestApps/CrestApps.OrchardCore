namespace CrestApps.AI;

/// <summary>
/// Interface for searching document embeddings in various index providers.
/// Implementations should be registered as keyed services using the provider name.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Searches for document chunks that are similar to the provided embedding vector.
    /// </summary>
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string referenceId,
        string referenceType,
        int topN,
        CancellationToken cancellationToken = default);
}
