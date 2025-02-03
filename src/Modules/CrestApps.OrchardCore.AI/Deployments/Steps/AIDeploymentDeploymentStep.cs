using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

public class AIDeploymentDeploymentStep : DeploymentStep
{
    public AIDeploymentDeploymentStep()
    {
        Name = AIDeploymentStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] DeploymentNames { get; set; }
}
