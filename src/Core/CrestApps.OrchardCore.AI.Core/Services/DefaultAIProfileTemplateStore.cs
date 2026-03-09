using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileTemplateStore : NamedDocumentCatalog<AIProfileTemplate, AIProfileTemplateIndex>
{
    public DefaultAIProfileTemplateStore(ISession session)
        : base(session)
    {
        CollectionName = AIConstants.AICollectionName;
    }
}
