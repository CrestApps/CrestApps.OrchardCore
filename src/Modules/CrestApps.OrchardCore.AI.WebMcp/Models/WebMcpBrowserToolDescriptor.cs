namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// A page tool as the chat bridge script reports it, before it is checked.
/// </summary>
public sealed class WebMcpBrowserToolDescriptor
{
    /// <summary>
    /// Gets or sets the function name of the tool.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the human-readable title of the tool.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the tool does, for the AI model.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema of the tool's input, as JSON text.
    /// </summary>
    public string InputSchema { get; set; }
}
