using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileTemplateStore : NamedSourceDocumentCatalog<AIProfileTemplate, AIProfileTemplateIndex>
{
    public DefaultAIProfileTemplateStore(ISession session)
    : base(session, AIConstants.AICollectionName)
    {
    }
}
