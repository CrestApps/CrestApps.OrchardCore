namespace CrestApps.OrchardCore.AI.Mcp.Deployments.ViewModels;

/// <summary>
/// Represents the view model for display mcp prompt deployment step.
/// </summary>
public class DisplayMcpPromptDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the names.
    /// </summary>
    public IEnumerable<string> Names { get; set; }
}
