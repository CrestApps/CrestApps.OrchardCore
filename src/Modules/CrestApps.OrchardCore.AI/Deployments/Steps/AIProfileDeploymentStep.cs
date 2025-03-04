using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIProfileDeploymentStep : DeploymentStep
{
    public AIProfileDeploymentStep()
    {
        Name = AIProfileStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] ProfileNames { get; set; }
}
