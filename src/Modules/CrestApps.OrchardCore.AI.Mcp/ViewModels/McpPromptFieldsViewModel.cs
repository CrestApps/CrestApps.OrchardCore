namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpPromptFieldsViewModel
{
    public string Name { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public List<McpPromptArgumentViewModel> Arguments { get; set; } = [];
}

public class McpPromptArgumentViewModel
{
    public string Name { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public bool Required { get; set; }
}
