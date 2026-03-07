using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

public interface IAIDocumentChunkStore : ICatalog<AIDocumentChunk>
{
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId);

    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType);

    Task DeleteByDocumentIdAsync(string documentId);
}
