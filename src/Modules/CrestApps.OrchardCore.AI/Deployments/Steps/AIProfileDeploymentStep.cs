using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIProfileDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDeploymentStep"/> class.
    /// </summary>
    public AIProfileDeploymentStep()
    {
        Name = AIProfileStep.StepKey;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the profile names.
    /// </summary>
    public string[] ProfileNames { get; set; }
}
