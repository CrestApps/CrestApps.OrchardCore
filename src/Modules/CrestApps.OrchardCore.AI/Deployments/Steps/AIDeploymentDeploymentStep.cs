using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI deployments.
/// </summary>
public sealed class AIDeploymentDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDeploymentStep"/> class.
    /// </summary>
    public AIDeploymentDeploymentStep()
    {
        Name = AIDeploymentStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public AIDeploymentDeploymentStep(IStringLocalizer<AIDeploymentDeploymentStep> S)
        : this()
    {
        Category = S["Artificial Intelligence"];
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
