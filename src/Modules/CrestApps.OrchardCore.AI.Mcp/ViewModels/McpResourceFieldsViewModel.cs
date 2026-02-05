using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class McpResourceFieldsViewModel
{
    public string Uri { get; set; }

    public string Name { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string MimeType { get; set; }

    public string DisplayText { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the URI patterns for the resource type, to display as help text in the UI.
    /// </summary>
    [BindNever]
    public string[] UriPatterns { get; set; } = [];
}
