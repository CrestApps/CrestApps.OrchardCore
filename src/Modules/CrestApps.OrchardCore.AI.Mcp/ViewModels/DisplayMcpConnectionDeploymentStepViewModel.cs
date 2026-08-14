namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for display mcp connection deployment step.
/// </summary>
public class DisplayMcpConnectionDeploymentStepViewModel
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
