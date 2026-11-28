using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The names of values a consumer may keep in a response's properties that Surveys reads, and how to read them back.
/// </summary>
public static class SurveyResponseProperties
{
    /// <summary>
    /// A local URL the ending and review pages link back to, such as the course item a learner came from.
    /// </summary>
    public const string ReturnUrl = "ReturnUrl";

    /// <summary>
    /// The text of the link back to <see cref="ReturnUrl"/>.
    /// </summary>
    public const string ReturnText = "ReturnText";

    /// <summary>
    /// Reads a text property of a response. A value read back from storage may be a JSON element or node rather than a
    /// string, so every shape is accepted.
    /// </summary>
    /// <param name="response">The response, or <see langword="null"/>.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    public static string ReadString(SurveyResponse response, string name)
    {
        if (response?.Properties is null || string.IsNullOrEmpty(name) || !response.Properties.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            null => null,
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => element.ToString(),
            JsonValue node when node.TryGetValue<string>(out var text) => text,
            JsonNode node => node.ToJsonString(),
            _ => value.ToString(),
        };
    }
}
