using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileDocumentStore : DocumentCatalog<AIProfileDocument, AIProfileDocumentIndex>, IAIProfileDocumentStore
{
    public DefaultAIProfileDocumentStore(ISession session)
        : base(session)
    {
        CollectionName = AIConstants.CollectionName;
    }

    public async Task<IReadOnlyCollection<AIProfileDocument>> GetDocuments(string profileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileId);

        return (await Session.Query<AIProfileDocument, AIProfileDocumentIndex>(x => x.ProfileId == profileId, CollectionName).ListAsync()).ToArray();
    }
}
