using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for mcp resource fields.
/// </summary>
public class McpResourceFieldsViewModel
{
    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the mime type.
    /// </summary>
    public string MimeType { get; set; }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the source.
    /// </summary>
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

/// <summary>
/// Represents the view model for mcp resource variable.
/// </summary>
public class McpResourceVariableViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }
}
