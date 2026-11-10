using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Currencies;

namespace CrestApps.OrchardCore.AgenticCommerce.Endpoints;

/// <summary>
/// Reads and writes the JSON values the agentic commerce protocols share.
/// </summary>
public static class AgentJson
{
    /// <summary>
    /// Returns an amount in a currency's smallest unit, as every protocol sends money.
    /// </summary>
    /// <param name="amount">The amount, in major units.</param>
    /// <param name="currency">The currency.</param>
    public static long ToMinor(decimal amount, string currency)
        => CurrencyScale.ToMinorUnits(amount, currency ?? "USD");

    /// <summary>
    /// Returns a time as RFC 3339, in UTC.
    /// </summary>
    /// <param name="utc">The time.</param>
    public static string ToRfc3339(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns a string member, or <see langword="null"/>.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="name">The member's name.</param>
    public static string String(JsonNode node, string name)
        => node is JsonObject json && json[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;

    /// <summary>
    /// Returns an integer member, or <see langword="null"/>.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="name">The member's name.</param>
    public static int? Int(JsonNode node, string name)
    {
        if (node is not JsonObject json || json[name] is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetValue<int>(out var number)
            ? number
            : value.TryGetValue<decimal>(out var dec) && dec >= int.MinValue && dec <= int.MaxValue ? (int)Math.Ceiling(dec) : null;
    }

    /// <summary>
    /// Returns a boolean member, or <see langword="null"/>.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="name">The member's name.</param>
    public static bool? Bool(JsonNode node, string name)
        => node is JsonObject json && json[name] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? value.GetValue<bool>()
            : null;

    /// <summary>
    /// Returns whether an object has a member, even one set to null.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="name">The member's name.</param>
    public static bool Has(JsonNode node, string name)
        => node is JsonObject json && json.ContainsKey(name);

    /// <summary>
    /// Returns the string items of an array member.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="name">The member's name.</param>
    public static List<string> Strings(JsonNode node, string name)
        => node is JsonObject json && json[name] is JsonArray array
            ? array.Where(item => item is JsonValue value && value.GetValueKind() == JsonValueKind.String).Select(item => item.GetValue<string>()).ToList()
            : null;

    /// <summary>
    /// Adds a member only when it has a value.
    /// </summary>
    /// <param name="json">The object.</param>
    /// <param name="name">The member's name.</param>
    /// <param name="value">The value.</param>
    public static void AddIf(JsonObject json, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            json[name] = value;
        }
    }
}
