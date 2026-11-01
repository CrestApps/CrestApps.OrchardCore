using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The answer of the endpoint that calls a tool, in the shape of an MCP tool result.
/// </summary>
public sealed class WebMcpToolCallResponse
{
    /// <summary>
    /// Gets the parts of the answer.
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<WebMcpTextContent> Content { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the answer reports a failure.
    /// </summary>
    [JsonPropertyName("isError")]
    public bool IsError { get; init; }
}
