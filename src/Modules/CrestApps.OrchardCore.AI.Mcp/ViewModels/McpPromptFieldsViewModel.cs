using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for mcp prompt fields.
/// </summary>
public class McpPromptFieldsViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the arguments.
    /// </summary>
    public List<McpPromptArgumentViewModel> Arguments { get; set; } = [];

    /// <summary>
    /// Gets or sets the messages that make up the body of this prompt.
    /// </summary>
    public List<McpPromptMessageViewModel> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }
}

/// <summary>
/// Represents the view model for an mcp prompt message.
/// </summary>
public class McpPromptMessageViewModel
{
    /// <summary>
    /// Gets or sets the role. Only "user" and "assistant" are supported.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the content. May contain <c>{{argName}}</c> placeholders.
    /// </summary>
    public string Content { get; set; }
}

/// <summary>
/// Represents the view model for mcp prompt argument.
/// </summary>
public class McpPromptArgumentViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the required.
    /// </summary>
    public bool Required { get; set; }
}
