using CrestApps.OrchardCore.AI.DataSources.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

/// <summary>
/// Represents a deployment step that exports AI data sources.
/// </summary>
public sealed class AIDataSourceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AIDataSourceDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AIDataSourceDeploymentStep>("AI Data Sources");

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDeploymentStep"/> class.
    /// </summary>
    public AIDataSourceDeploymentStep()
    {
        Name = AIDataSourceStep.StepKey;
        Category = _category;
        Title = _title;
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
