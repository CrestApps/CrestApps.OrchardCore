using CrestApps.OrchardCore.AI.DataSources.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

internal sealed class AIDataSourceDeploymentStep : DeploymentStep
{
    public AIDataSourceDeploymentStep()
    {
        Name = AIDataSourceStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] SourceIds { get; set; }
}
