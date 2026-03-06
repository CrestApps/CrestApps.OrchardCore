using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDocumentChunkStore : DocumentCatalog<AIDocumentChunk, AIDocumentChunkIndex>, IAIDocumentChunkStore
{
    public DefaultAIDocumentChunkStore(ISession session)
        : base(session)
    {
        CollectionName = AIConstants.AIDocsCollectionName;
    }

    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        return (await Session.Query<AIDocumentChunk, AIDocumentChunkIndex>(
            x => x.AIDocumentId == documentId,
            CollectionName).ListAsync()).ToArray();
    }

    public async Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(string referenceId, string referenceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        return (await Session.Query<AIDocumentChunk, AIDocumentChunkIndex>(
            x => x.ReferenceId == referenceId && x.ReferenceType == referenceType,
            CollectionName).ListAsync()).ToArray();
    }

    public async Task DeleteByDocumentIdAsync(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        var chunks = await Session.Query<AIDocumentChunk, AIDocumentChunkIndex>(
            x => x.AIDocumentId == documentId,
            CollectionName).ListAsync();

        foreach (var chunk in chunks)
        {
            Session.Delete(chunk, CollectionName);
        }
    }
}
