using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.ViewModels;

public class McpConnectionStepViewModel
{
    public bool IncludeAll { get; set; }

    public SelectListItem[] Connections { get; set; }
}
