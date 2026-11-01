using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// Describes a tool a browser agent may call through WebMCP.
/// </summary>
public sealed class WebMcpToolDescriptor
{
    /// <summary>
    /// Gets or sets the function name the agent calls the tool by.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the human-readable title of the tool.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the tool does, for the agent.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema of the tool's input.
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}
