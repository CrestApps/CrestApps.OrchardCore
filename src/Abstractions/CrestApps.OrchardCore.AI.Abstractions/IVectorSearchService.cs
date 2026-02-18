using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Interface for searching document embeddings in various index providers.
/// Implementations should be registered as keyed services using the provider name.
/// For example: services.AddKeyedScoped&lt;IEmbeddingSearchService, ElasticsearchEmbeddingSearchService&gt;(ElasticsearchConstants.ProviderName)
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Searches for document chunks that are similar to the provided embedding vector.
    /// </summary>
    /// <param name="indexProfile">The index profile to search.</param>
    /// <param name="embedding">The embedding vector to search for similar documents.</param>
    /// <param name="referenceId">The reference ID (e.g., AI Profile ID or Chat Interaction ID) to filter results by.</param>
    /// <param name="referenceType">The reference type (e.g., "profile" or "chatinteraction") to filter results by.</param>
    /// <param name="topN">The maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching document chunks with their similarity scores.</returns>
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IndexProfile indexProfile,
        float[] embedding,
        string referenceId,
        string referenceType,
        int topN,
        CancellationToken cancellationToken = default);
}
