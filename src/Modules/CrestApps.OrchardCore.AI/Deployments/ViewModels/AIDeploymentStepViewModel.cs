namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Represents the view model for AI deployment step.
/// </summary>
public class AIDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the deployment names.
    /// </summary>
    public string[] DeploymentNames { get; set; }

    /// <summary>
    /// Gets or sets the all deployment name.
    /// </summary>
    public string[] AllDeploymentName { get; set; }
}
