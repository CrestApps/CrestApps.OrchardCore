using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDocumentStore : DocumentCatalog<AIDocument, AIDocumentIndex>, IAIDocumentStore
{
    public DefaultAIDocumentStore(ISession session)
        : base(session)
    {
        CollectionName = AIConstants.CollectionName;
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
