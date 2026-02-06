using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpResourceFieldsViewModel
{
    public string Path { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string MimeType { get; set; }

    public string DisplayText { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string Source { get; set; }

    /// <summary>
    /// <summary>
    /// Gets or sets the system-generated item identifier.
    /// </summary>
    [BindNever]
    public string McpPromptItemId { get; set; }

    /// <summary>
    /// Gets or sets the URI path patterns for the resource type, to display as help text in the UI.
    /// </summary>
    [BindNever]
    public string[] UriPatterns { get; set; } = [];
}
