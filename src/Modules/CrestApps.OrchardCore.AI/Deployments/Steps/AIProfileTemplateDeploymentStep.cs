using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI profile templates.
/// </summary>
public sealed class AIProfileTemplateDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDeploymentStep"/> class.
    /// </summary>
    public AIProfileTemplateDeploymentStep()
    {
        Name = AIProfileTemplateStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public AIProfileTemplateDeploymentStep(IStringLocalizer<AIProfileTemplateDeploymentStep> S)
        : this()
    {
        Category = S["Artificial Intelligence"];
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
