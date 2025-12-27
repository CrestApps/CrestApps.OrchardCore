namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a handler for document indexing events.
/// Implementations can store document embeddings in various backends (Elasticsearch, Azure AI Search, etc.).
/// </summary>
public interface IDocumentIndexHandler
{
    /// <summary>
    /// Gets the priority of this handler. Lower values run first.
    /// </summary>
    int Priority => 0;

    /// <summary>
    /// Initializes the index if it doesn't exist.
    /// Called during application startup or when the feature is enabled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a document by embedding its content in chunks.
    /// </summary>
    /// <param name="context">The document indexing context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexDocumentAsync(DocumentIndexContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all indexed chunks for a specific document.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveDocumentAsync(string sessionId, string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all indexed chunks for a specific session.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for relevant document chunks based on a query.
    /// </summary>
    /// <param name="sessionId">The session/interaction identifier.</param>
    /// <param name="queryEmbedding">The embedding of the search query.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of relevant document chunks.</returns>
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        string sessionId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for document indexing operations.
/// </summary>
public sealed class DocumentIndexContext
{
    /// <summary>
    /// Gets or sets the session/interaction identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the document chunks with embeddings.
    /// </summary>
    public IList<AI.Models.DocumentChunk> Chunks { get; set; } = [];
}

/// <summary>
/// Represents a search result from the document index.
/// </summary>
public sealed class DocumentChunkSearchResult
{
    /// <summary>
    /// Gets or sets the document chunk.
    /// </summary>
    public AI.Models.DocumentChunk Chunk { get; set; }

    /// <summary>
    /// Gets or sets the similarity score.
    /// </summary>
    public float Score { get; set; }
}
