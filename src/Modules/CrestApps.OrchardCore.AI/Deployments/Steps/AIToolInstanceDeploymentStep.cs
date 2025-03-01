using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIToolInstanceDeploymentStep : DeploymentStep
{
    public AIToolInstanceDeploymentStep()
    {
        Name = AIToolInstanceStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] InstanceIds { get; set; }
}
