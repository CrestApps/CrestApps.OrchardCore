using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Reads the JSON a model answers with, which may come wrapped in a Markdown code block or prose.
/// </summary>
public static class SurveysAIJson
{
    /// <summary>
    /// Reads the first JSON object of a model's answer.
    /// </summary>
    /// <param name="text">The answer.</param>
    /// <returns>The object, or <see langword="null"/> when there is none.</returns>
    public static JsonObject ReadObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text.Substring(start, end - start + 1)) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a string, cut to a length.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="maxLength">The longest string kept.</param>
    /// <returns>The string, or <see langword="null"/>.</returns>
    public static string String(JsonNode node, int maxLength)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();

        return text.Length > maxLength ? text.Substring(0, maxLength) : text;
    }

    /// <summary>
    /// Reads a number written as a number or as text.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static decimal? Number(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<decimal>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }
}
