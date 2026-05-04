using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class DeleteAIDeploymentDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIDeploymentDeploymentStep"/> class.
    /// </summary>
    public DeleteAIDeploymentDeploymentStep()
    {
        Name = DeleteAIDeploymentStep.StepKey;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the deployment names.
    /// </summary>
    public string[] DeploymentNames { get; set; }
}
