using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIProfileTemplateDeploymentStep : DeploymentStep
{
    public AIProfileTemplateDeploymentStep()
    {
        Name = AIProfileTemplateStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] TemplateNames { get; set; }
}
