using CrestApps.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Manages agent platforms.
/// </summary>
public sealed class AgentPlatformManager : CatalogManager<AgentPlatform>, IAgentPlatformManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPlatformManager"/> class.
    /// </summary>
    /// <param name="store">The platform store.</param>
    /// <param name="handlers">The platform handlers.</param>
    /// <param name="logger">The logger.</param>
    public AgentPlatformManager(
        IAgentPlatformStore store,
        IEnumerable<ICatalogEntryHandler<AgentPlatform>> handlers,
        ILogger<CatalogManager<AgentPlatform>> logger)
        : base(store, handlers, logger)
    {
    }
}
