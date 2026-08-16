namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Represents the view model for the AI tool instance deployment step.
/// </summary>
public class AIToolInstanceDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether all tool instances are exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the tool instances.
    /// </summary>
    public AIToolInstanceEntryViewModel[] Instances { get; set; }
}
