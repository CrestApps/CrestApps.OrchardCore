using CrestApps.Core.AI;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDocumentStore : DocumentCatalog<AIDocument, AIDocumentIndex>, IAIDocumentStore
{
    public DefaultAIDocumentStore(ISession session)
    : base(session, AIConstants.AIDocsCollectionName)
    {
    }

    public async Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(string referenceId, string referenceType)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        return (await Session.Query<AIDocument, AIDocumentIndex>(
            x => x.ReferenceId == referenceId && x.ReferenceType == referenceType,
            CollectionName).ListAsync()).ToArray();
    }
}
