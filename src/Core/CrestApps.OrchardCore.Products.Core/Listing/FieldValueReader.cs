using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Reads stored field values and field settings as JSON, for an <see cref="IProductAttributeValueProvider"/>.
/// </summary>
/// <remarks>
/// The attribute value providers read the standard Orchard fields through their stored JSON rather than their CLR
/// types, so the Products modules need no reference to the field modules and listing keeps working whichever of them a
/// site enables.
/// </remarks>
public static class FieldValueReader
{
    public static string GetString(JsonObject value, string propertyName)
        => value?[propertyName] is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    public static decimal? GetDecimal(JsonObject value, string propertyName)
    {
        if (value?[propertyName] is not JsonValue node)
        {
            return null;
        }

        if (node.TryGetValue<decimal>(out var number))
        {
            return number;
        }

        return node.TryGetValue<string>(out var text) &&
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    public static bool? GetBoolean(JsonObject value, string propertyName)
        => value?[propertyName] is JsonValue node && node.TryGetValue<bool>(out var flag) ? flag : null;

    public static DateTime? GetDate(JsonObject value, string propertyName)
    {
        if (value?[propertyName] is not JsonValue node)
        {
            return null;
        }

        if (node.TryGetValue<DateTime>(out var date))
        {
            return date;
        }

        return node.TryGetValue<string>(out var text) &&
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)
            ? date
            : null;
    }

    public static IEnumerable<string> GetStrings(JsonObject value, string propertyName)
    {
        if (value?[propertyName] is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var text) ? text : null)
            .Where(text => !string.IsNullOrWhiteSpace(text));
    }

    /// <summary>
    /// Reads the name/value options a field's settings define, such as a predefined list's entries.
    /// </summary>
    /// <param name="field">The field definition.</param>
    /// <param name="settingsName">The settings object that holds the options.</param>
    /// <returns>The options in their defined order.</returns>
    public static IReadOnlyList<(string Name, string Value)> GetOptions(ContentPartFieldDefinition field, string settingsName)
    {
        if (field.Settings?[settingsName] is not JsonObject settings ||
            GetProperty(settings, "Options") is not JsonArray options)
        {
            return [];
        }

        var result = new List<(string Name, string Value)>();

        foreach (var option in options.OfType<JsonObject>())
        {
            var optionValue = GetProperty(option, "Value") is JsonValue valueNode && valueNode.TryGetValue<string>(out var text) ? text : null;
            var name = GetProperty(option, "Name") is JsonValue nameNode && nameNode.TryGetValue<string>(out var label) ? label : null;

            if (!string.IsNullOrEmpty(optionValue))
            {
                result.Add((string.IsNullOrEmpty(name) ? optionValue : name, optionValue));
            }
        }

        return result;
    }

    public static string GetSettingString(ContentPartFieldDefinition field, string settingsName, string propertyName)
        => field.Settings?[settingsName] is JsonObject settings &&
            GetProperty(settings, propertyName) is JsonValue node &&
            node.TryGetValue<string>(out var text)
            ? text
            : null;

    public static string GetEditor(ContentPartFieldDefinition field)
        => GetSettingString(field, "ContentPartFieldSettings", "Editor");

    // Settings written by Orchard's editors and by hand-written recipes differ in casing ("Options" or
    // "options"), so look the property up either way.
    private static JsonNode GetProperty(JsonObject value, string propertyName)
    {
        if (value.TryGetPropertyValue(propertyName, out var node))
        {
            return node;
        }

        foreach (var property in value)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    public static string ToInvariant(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.General);
}
