using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

public interface IAIDocumentChunkStore : ICatalog<AIDocumentChunk>
{
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId);

    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType);

    Task DeleteByDocumentIdAsync(string documentId);
}
