using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Seo.AI;

/// <summary>
/// Reads the arguments of the structured data AI tools, which models send as JSON values or as text.
/// </summary>
internal static class SchemaOrgToolArguments
{
    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The text, or <see langword="null"/> when it is missing or empty.</returns>
    public static string GetString(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => element.GetRawText(),
            _ => value.ToString(),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a boolean argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing.</returns>
    public static bool? GetBoolean(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool flag => flag,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            _ => bool.TryParse(GetString(arguments, name), out var parsed) ? parsed : null,
        };
    }

    /// <summary>
    /// Reads an argument as JSON and deserializes it.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <param name="options">The serializer options.</param>
    /// <param name="result">The value.</param>
    /// <returns><see langword="false"/> when the argument is present but cannot be read.</returns>
    public static bool TryGetJson<T>(AIFunctionArguments arguments, string name, JsonSerializerOptions options, out T result)
    {
        result = default;

        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return true;
        }

        try
        {
            result = value switch
            {
                JsonElement { ValueKind: JsonValueKind.String } element => JsonSerializer.Deserialize<T>(element.GetString(), options),
                JsonElement element => element.Deserialize<T>(options),
                string text => JsonSerializer.Deserialize<T>(text, options),
                _ => JsonSerializer.SerializeToElement(value, options).Deserialize<T>(options),
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
