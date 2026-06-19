using CrestApps.OrchardCore.AI.DataSources.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

/// <summary>
/// Represents a deployment step that exports AI data sources.
/// </summary>
public sealed class AIDataSourceDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDeploymentStep"/> class.
    /// </summary>
    public AIDataSourceDeploymentStep()
    {
        Name = AIDataSourceStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public AIDataSourceDeploymentStep(IStringLocalizer<AIDataSourceDeploymentStep> S)
        : this()
    {
        Category = S["Artificial Intelligence"];
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the source ids.
    /// </summary>
    public string[] SourceIds { get; set; }
}
