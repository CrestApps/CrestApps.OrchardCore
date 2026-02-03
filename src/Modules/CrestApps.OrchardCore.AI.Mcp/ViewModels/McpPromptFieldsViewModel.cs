using CrestApps.OrchardCore.AI.Mcp.Core.Models;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpPromptFieldsViewModel
{
    public string DisplayText { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public List<McpPromptArgumentViewModel> Arguments { get; set; } = [];

    public List<McpPromptMessageViewModel> Messages { get; set; } = [];
}

public class McpPromptArgumentViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool IsRequired { get; set; }
}

public class McpPromptMessageViewModel
{
    public string Role { get; set; }

    public string Content { get; set; }
}
