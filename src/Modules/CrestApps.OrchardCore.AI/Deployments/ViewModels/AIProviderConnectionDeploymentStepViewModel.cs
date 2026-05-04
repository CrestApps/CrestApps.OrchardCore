namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Represents the view model for AI provider connection deployment step.
/// </summary>
public class AIProviderConnectionDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the connections.
    /// </summary>
    public AIProviderConnectionEntryViewModel[] Connections { get; set; }
}
