using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

public sealed class AIProviderConnectionDeploymentStep : DeploymentStep
{
    public AIProviderConnectionDeploymentStep()
    {
        Name = AIProviderConnectionsStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] ConnectionIds { get; set; }
}
