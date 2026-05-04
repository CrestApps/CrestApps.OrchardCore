using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="ICatalogManager{AIDataSource}"/> for managing AI data source lifecycle.
/// </summary>
public sealed class DefaultAIDataSourceManager : CatalogManager<AIDataSource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIDataSourceManager"/> class.
    /// </summary>
    /// <param name="store">The data source store for persistence.</param>
    /// <param name="handlers">The catalog entry handlers for data source lifecycle events.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultAIDataSourceManager(
        IAIDataSourceStore store,
        IEnumerable<ICatalogEntryHandler<AIDataSource>> handlers,
        ILogger<DefaultAIDataSourceManager> logger)
    : base(store, handlers, logger)
    {
    }
}
