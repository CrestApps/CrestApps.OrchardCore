using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpResourceStepViewModel
{
    public bool IncludeAll { get; set; }

    public SelectListItem[] Resources { get; set; }
}
