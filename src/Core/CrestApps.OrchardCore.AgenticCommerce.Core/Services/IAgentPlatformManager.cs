using CrestApps.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Manages agent platforms, so their handlers run.
/// </summary>
public interface IAgentPlatformManager : ICatalogManager<AgentPlatform>
{
}
