using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for mcp resource step.
/// </summary>
public class McpResourceStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the resources.
    /// </summary>
    public SelectListItem[] Resources { get; set; }
}
