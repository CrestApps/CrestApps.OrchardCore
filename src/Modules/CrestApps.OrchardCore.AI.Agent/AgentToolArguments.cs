using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Reads the arguments of the agent tools that the shared argument helpers do not cover.
/// </summary>
internal static class AgentToolArguments
{
    /// <summary>
    /// Reads an argument that holds a JSON object. A model sends it as an object or as JSON text; both are accepted.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">A copy of the object, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a JSON object.</returns>
    public static bool TryReadJsonObject(AIFunctionArguments arguments, string key, out JsonObject value)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        value = null;

        if (!arguments.TryGetFirst(key, out var argument))
        {
            return true;
        }

        try
        {
            var node = argument switch
            {
                JsonObject jsonObject => jsonObject.DeepClone(),
                JsonElement element when element.ValueKind == JsonValueKind.Object => JsonObject.Create(element),
                JsonElement element when element.ValueKind == JsonValueKind.String => JsonNode.Parse(element.GetString()),
                string text => JsonNode.Parse(text),
                _ => null,
            };

            value = node as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        return value is not null;
    }

    /// <summary>
    /// Reads an argument that holds a list of texts, such as IDs or names.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="values">The texts without blanks and repeats, in the given order, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a list of texts.</returns>
    public static bool TryReadStrings(AIFunctionArguments arguments, string key, out List<string> values)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        values = null;

        if (!arguments.TryGetFirst(key, out _))
        {
            return true;
        }

        if (!arguments.TryGetFirst<string[]>(key, out var given) || given is null)
        {
            return false;
        }

        values = [];

        foreach (var value in given)
        {
            var trimmed = value?.Trim();

            if (!string.IsNullOrEmpty(trimmed) && !values.Contains(trimmed, StringComparer.Ordinal))
            {
                values.Add(trimmed);
            }
        }

        return true;
    }

    /// <summary>
    /// Reads an argument that holds a JSON array. A model sends it as an array or as JSON text; both are accepted.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">A copy of the array, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a JSON array.</returns>
    public static bool TryReadJsonArray(AIFunctionArguments arguments, string key, out JsonArray value)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        value = null;

        if (!arguments.TryGetFirst(key, out var argument))
        {
            return true;
        }

        try
        {
            var node = argument switch
            {
                JsonArray jsonArray => jsonArray.DeepClone(),
                JsonElement element when element.ValueKind == JsonValueKind.Array => JsonArray.Create(element),
                JsonElement element when element.ValueKind == JsonValueKind.String => JsonNode.Parse(element.GetString()),
                string text => JsonNode.Parse(text),
                _ => null,
            };

            value = node as JsonArray;
        }
        catch (JsonException)
        {
            return false;
        }

        return value is not null;
    }
}
