using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// A text part of an MCP tool result.
/// </summary>
public sealed class WebMcpTextContent
{
    /// <summary>
    /// Gets the kind of the part, which is always <c>text</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; } = "text";

    /// <summary>
    /// Gets the text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; init; }
}
