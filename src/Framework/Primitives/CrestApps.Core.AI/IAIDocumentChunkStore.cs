using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI;

/// <summary>
/// Provides persistent storage for AI document chunks, supporting CRUD operations,
/// retrieval by parent document, retrieval by reference entity, and bulk deletion.
/// </summary>
public interface IAIDocumentChunkStore : ICatalog<AIDocumentChunk>
{
    /// <summary>
    /// Asynchronously retrieves all chunks belonging to the specified AI document.
    /// </summary>
    /// <param name="documentId">The unique identifier of the parent AI document.</param>
    /// <returns>A read-only collection of document chunks.</returns>
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId);

    /// <summary>
    /// Asynchronously retrieves all chunks associated with the specified reference entity.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the reference entity.</param>
    /// <param name="referenceType">The type of the reference entity.</param>
    /// <returns>A read-only collection of document chunks matching the reference.</returns>
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType);

    /// <summary>
    /// Asynchronously deletes all chunks belonging to the specified AI document.
    /// </summary>
    /// <param name="documentId">The unique identifier of the parent AI document whose chunks should be deleted.</param>
    Task DeleteByDocumentIdAsync(string documentId);
}
