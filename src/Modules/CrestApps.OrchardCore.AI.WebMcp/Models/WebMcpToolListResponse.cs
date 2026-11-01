using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The answer of the endpoint that lists the tools the current user may call.
/// </summary>
public sealed class WebMcpToolListResponse
{
    /// <summary>
    /// Gets the tools.
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<WebMcpToolDescriptor> Tools { get; init; } = [];
}
