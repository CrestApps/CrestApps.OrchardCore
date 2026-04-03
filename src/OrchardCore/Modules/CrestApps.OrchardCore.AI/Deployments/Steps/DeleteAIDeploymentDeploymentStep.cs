using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class DeleteAIDeploymentDeploymentStep : DeploymentStep
{
    public DeleteAIDeploymentDeploymentStep()
    {
        Name = DeleteAIDeploymentStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] DeploymentNames { get; set; }
}
