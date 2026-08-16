using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI tool instances.
/// </summary>
public sealed class AIToolInstanceDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentStep"/> class.
    /// </summary>
    public AIToolInstanceDeploymentStep()
    {
        Name = AIToolInstanceStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public AIToolInstanceDeploymentStep(IStringLocalizer<AIToolInstanceDeploymentStep> S)
        : this()
    {
        Category = S["Artificial Intelligence"];
    }

    /// <summary>
    /// Gets or sets a value indicating whether all tool instances are exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the tool instances to export.
    /// </summary>
    public string[] InstanceIds { get; set; }
}
