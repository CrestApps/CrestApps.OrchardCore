using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.ViewModels;

/// <summary>
/// Represents the view model for mcp connection step.
/// </summary>
public class McpConnectionStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the connections.
    /// </summary>
    public SelectListItem[] Connections { get; set; }
}
