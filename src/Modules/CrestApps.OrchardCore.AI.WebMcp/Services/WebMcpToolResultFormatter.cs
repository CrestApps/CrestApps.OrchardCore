using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Turns what an AI tool answers into the text a browser agent reads.
/// </summary>
internal static class WebMcpToolResultFormatter
{
    /// <summary>
    /// Formats a tool's answer: text as it is, the text of AI contents, a JSON string as its text, and anything else as JSON.
    /// </summary>
    /// <param name="result">The tool's answer.</param>
    /// <returns>The text.</returns>
    public static string Format(object result)
    {
        return result switch
        {
            null => string.Empty,
            string text => text,
            JsonElement element => element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText(),
            JsonNode node => node.ToJsonString(),
            TextContent content => content.Text,
            IEnumerable<AIContent> contents => string.Join('\n', contents.OfType<TextContent>().Select(content => content.Text)),
            _ => JsonSerializer.Serialize(result, result.GetType()),
        };
    }
}
