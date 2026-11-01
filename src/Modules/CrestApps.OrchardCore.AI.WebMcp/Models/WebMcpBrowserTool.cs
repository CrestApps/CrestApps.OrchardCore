using System.Text.Json;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// A checked page tool the AI chat may call in the user's browser.
/// </summary>
public sealed class WebMcpBrowserTool
{
    /// <summary>
    /// Gets the function name of the tool.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the human-readable title of the tool.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Gets what the tool does, for the AI model.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the JSON schema of the tool's input.
    /// </summary>
    public JsonElement InputSchema { get; init; }
}
