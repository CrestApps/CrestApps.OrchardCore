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
}
