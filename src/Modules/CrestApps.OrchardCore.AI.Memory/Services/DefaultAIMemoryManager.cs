using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Memory.Services;

/// <summary>
/// Represents the default AI memory manager.
/// </summary>
public sealed class DefaultAIMemoryManager : CatalogManager<AIMemoryEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIMemoryManager"/> class.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public DefaultAIMemoryManager(
        IAIMemoryStore catalog,
        IEnumerable<ICatalogEntryHandler<AIMemoryEntry>> handlers,
        ILogger<DefaultAIMemoryManager> logger)
    : base(catalog, handlers, logger)
    {
    }
}
