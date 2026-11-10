using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.AgenticCommerce.Recipes;

internal sealed class AgentPlatformsStep : CatalogRecipeStep<AgentPlatform>
{
    public AgentPlatformsStep(IAgentPlatformManager manager)
        : base(AgentPlatformsDeploymentStep.StepName, "Platforms", manager)
    {
    }
}
