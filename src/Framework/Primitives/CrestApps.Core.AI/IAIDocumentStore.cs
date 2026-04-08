using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI;

/// <summary>
/// Provides persistent storage for AI documents, supporting CRUD operations
/// and retrieval of documents associated with a specific reference entity.
/// </summary>
public interface IAIDocumentStore : ICatalog<AIDocument>
{
    /// <summary>
    /// Asynchronously retrieves all AI documents associated with the specified reference entity.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the reference entity (e.g., data source or profile).</param>
    /// <param name="referenceType">The type of the reference entity (e.g., "DataSource", "ChatInteraction").</param>
    /// <returns>A read-only collection of documents matching the reference.</returns>
    Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType);
}
