using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
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
