using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

public class AIChatProfileDeploymentStep : DeploymentStep
{
    public AIChatProfileDeploymentStep()
    {
        Name = AIChatProfileStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] ProfileNames { get; set; }
}
