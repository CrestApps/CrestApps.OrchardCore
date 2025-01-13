using CrestApps.OrchardCore.OpenAI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.OpenAI.Deployments.Steps;

public class OpenAIChatProfileDeploymentStep : DeploymentStep
{
    public OpenAIChatProfileDeploymentStep()
    {
        Name = OpenAIChatProfileStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] ProfileNames { get; set; }
}
