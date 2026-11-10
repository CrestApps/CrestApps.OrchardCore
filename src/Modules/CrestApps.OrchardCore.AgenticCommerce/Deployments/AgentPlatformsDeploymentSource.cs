using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AgenticCommerce.Deployments;

internal sealed class AgentPlatformsDeploymentSource : CatalogDeploymentSource<AgentPlatformsDeploymentStep, AgentPlatform>
{
    public AgentPlatformsDeploymentSource(IAgentPlatformManager manager)
        : base("Platforms", manager)
    {
    }
}
