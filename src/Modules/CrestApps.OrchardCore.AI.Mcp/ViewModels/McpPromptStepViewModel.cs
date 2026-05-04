using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for mcp prompt step.
/// </summary>
public class McpPromptStepViewModel
{
    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the prompts.
    /// </summary>
    public SelectListItem[] Prompts { get; set; }
}
