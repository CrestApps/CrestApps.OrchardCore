using CrestApps.AI.Models;
using CrestApps.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDataSourceManager : CatalogManager<AIDataSource>
{
    public DefaultAIDataSourceManager(
        ICatalog<AIDataSource> store,
        IEnumerable<ICatalogEntryHandler<AIDataSource>> handlers,
        ILogger<DefaultAIDataSourceManager> logger)
        : base(store, handlers, logger)
    {
    }
}
