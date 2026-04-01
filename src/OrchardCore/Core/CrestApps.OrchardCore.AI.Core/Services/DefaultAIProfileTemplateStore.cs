using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Services;
using CrestApps.OrchardCore.AI.Core.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileTemplateStore : NamedSourceDocumentCatalog<AIProfileTemplate, AIProfileTemplateIndex>
{
    public DefaultAIProfileTemplateStore(ISession session)
    : base(session)
    {
        CollectionName = AIConstants.AICollectionName;
    }
}
