using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIProfileTemplateDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDeploymentStep"/> class.
    /// </summary>
    public AIProfileTemplateDeploymentStep()
    {
        Name = AIProfileTemplateStep.StepKey;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the template names.
    /// </summary>
    public string[] TemplateNames { get; set; }
}
