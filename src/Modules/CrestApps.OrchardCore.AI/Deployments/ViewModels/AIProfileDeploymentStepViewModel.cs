namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Represents the view model for AI profile deployment step.
/// </summary>
public class AIProfileDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the profile names.
    /// </summary>
    public string[] ProfileNames { get; set; }

    /// <summary>
    /// Gets or sets the all profile names.
    /// </summary>
    public string[] AllProfileNames { get; set; }
}
