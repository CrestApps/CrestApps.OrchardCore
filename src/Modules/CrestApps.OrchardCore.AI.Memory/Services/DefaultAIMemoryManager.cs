using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Memory.Services;

public sealed class DefaultAIMemoryManager : CatalogManager<AIMemoryEntry>
{
    public DefaultAIMemoryManager(
        IAIMemoryStore catalog,
        IEnumerable<ICatalogEntryHandler<AIMemoryEntry>> handlers,
        ILogger<DefaultAIMemoryManager> logger)
        : base(catalog, handlers, logger)
    {
    }
}
