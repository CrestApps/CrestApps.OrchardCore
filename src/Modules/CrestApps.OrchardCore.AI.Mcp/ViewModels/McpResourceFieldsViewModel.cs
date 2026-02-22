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
    /// Gets or sets the system-generated item identifier.
    /// </summary>
    [BindNever]
    public string McpPromptItemId { get; set; }

    /// <summary>
    /// Gets or sets a preview of the full constructed URI (e.g., "file://abc123/docs/{fileName}").
    /// </summary>
    [BindNever]
    public string UriPreview { get; set; }

    /// <summary>
    /// Gets or sets the supported variables for the resource type, to display as help text in the UI.
    /// </summary>
    [BindNever]
    public McpResourceVariableViewModel[] SupportedVariables { get; set; } = [];
}

public class McpResourceVariableViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }
}
