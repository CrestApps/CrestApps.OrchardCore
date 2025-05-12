using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIDataSourceDeploymentStep : DeploymentStep
{
    public AIDataSourceDeploymentStep()
    {
        Name = AIDataSourceStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] SourceIds { get; set; }
}
