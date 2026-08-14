namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Represents the view model for AI profile template deployment step.
/// </summary>
public class AIProfileTemplateDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the template names.
    /// </summary>
    public string[] TemplateNames { get; set; }

    /// <summary>
    /// Gets or sets the all template names.
    /// </summary>
    public string[] AllTemplateNames { get; set; }
}
