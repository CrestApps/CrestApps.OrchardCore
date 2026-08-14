using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI deployment deletion instructions.
/// </summary>
public sealed class DeleteAIDeploymentDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIDeploymentDeploymentStep"/> class.
    /// </summary>
    public DeleteAIDeploymentDeploymentStep()
    {
        Name = DeleteAIDeploymentStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIDeploymentDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public DeleteAIDeploymentDeploymentStep(IStringLocalizer<DeleteAIDeploymentDeploymentStep> S)
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
