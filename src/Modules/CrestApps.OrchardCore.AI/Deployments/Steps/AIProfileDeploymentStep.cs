using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI profiles.
/// </summary>
public sealed class AIProfileDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDeploymentStep"/> class.
    /// </summary>
    public AIProfileDeploymentStep()
    {
        Name = AIProfileStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public AIProfileDeploymentStep(IStringLocalizer<AIProfileDeploymentStep> S)
        : this()
    {
        Category = S["Artificial Intelligence"];
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
