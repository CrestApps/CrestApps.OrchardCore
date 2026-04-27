using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the default AI profile template store.
/// </summary>
public sealed class DefaultAIProfileTemplateStore : NamedSourceDocumentCatalog<AIProfileTemplate, AIProfileTemplateIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIProfileTemplateStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public DefaultAIProfileTemplateStore(ISession session)
    : base(session, AIConstants.AICollectionName)
    {
    }
}
