namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

/// <summary>
/// Represents the view model for AI data source deployment step.
/// </summary>
public class AIDataSourceDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the data sources.
    /// </summary>
    public AIDataSourceEntryViewModel[] DataSources { get; set; }
}
