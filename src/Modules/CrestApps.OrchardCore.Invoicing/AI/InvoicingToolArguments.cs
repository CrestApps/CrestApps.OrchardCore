using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Reads the list arguments of the credit note and estimate tools: an array of objects, sent by the model as a JSON array
/// or as JSON text, and the numbers inside it.
/// </summary>
internal static class InvoicingToolArguments
{
    /// <summary>
    /// Reads an array argument, given as a JSON array or as JSON text.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The items, or <see langword="null"/> when the argument is missing or not an array.</returns>
    public static IReadOnlyList<JsonElement> ReadArray(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var raw) || raw is null)
        {
            return null;
        }

        JsonElement element;

        try
        {
            element = raw switch
            {
                JsonElement json when json.ValueKind == JsonValueKind.String => JsonSerializer.Deserialize<JsonElement>(json.GetString()),
                JsonElement json => json,
                string text => JsonSerializer.Deserialize<JsonElement>(text),
                _ => JsonSerializer.SerializeToElement(raw),
            };
        }
        catch (JsonException)
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.Array ? [.. element.EnumerateArray()] : null;
    }

    /// <summary>
    /// Reads a number from a JSON value, given as a number or as text.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <param name="value">The number.</param>
    /// <returns>Whether it is a number.</returns>
    public static bool TryGetDecimal(JsonElement element, out decimal value)
    {
        value = 0m;

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }
}
