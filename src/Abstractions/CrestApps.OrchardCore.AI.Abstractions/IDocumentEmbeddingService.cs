namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a service for embedding and indexing documents.
/// </summary>
public interface IDocumentEmbeddingService
{
    /// <summary>
    /// Embeds and indexes a document for a session.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="content">The text content to embed and index.</param>
    /// <param name="providerName">The AI provider name for embedding.</param>
    /// <param name="connectionName">The connection name for the embedding provider.</param>
    /// <param name="deploymentName">The deployment/model name for embeddings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexDocumentAsync(
        string sessionId,
        string documentId,
        string fileName,
        string content,
        string providerName,
        string connectionName,
        string deploymentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the index.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveDocumentAsync(string sessionId, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all documents for a session from the index.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for relevant document chunks based on a query.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="query">The search query.</param>
    /// <param name="providerName">The AI provider name for embedding.</param>
    /// <param name="connectionName">The connection name for the embedding provider.</param>
    /// <param name="deploymentName">The deployment/model name for embeddings.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of relevant document chunks with scores.</returns>
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        string sessionId,
        string query,
        string providerName,
        string connectionName,
        string deploymentName,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
