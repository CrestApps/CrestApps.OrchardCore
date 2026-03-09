using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileTemplateManager : NamedCatalogManager<AIProfileTemplate>
{
    public DefaultAIProfileTemplateManager(
        INamedCatalog<AIProfileTemplate> catalog,
        IEnumerable<ICatalogEntryHandler<AIProfileTemplate>> handlers,
        ILogger<DefaultAIProfileTemplateManager> logger)
        : base(catalog, handlers, logger)
    {
    }
}
