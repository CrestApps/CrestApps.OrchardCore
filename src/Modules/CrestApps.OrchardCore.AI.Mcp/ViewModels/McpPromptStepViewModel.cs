using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpPromptStepViewModel
{
    public bool IncludeAll { get; set; }

    public SelectListItem[] Prompts { get; set; }
}
