using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Reads a field whose value is one property of its stored JSON (a string, number, boolean or array), such as
/// <c>TextField.Text</c> or <c>ContentPickerField.ContentItemIds</c>.
/// </summary>
internal sealed class JsonFieldValueReader : IContentFieldValueReader
{
    private readonly string[] _path;
    private readonly Func<ContentPartFieldDefinition, IEnumerable<ContentConditionOption>> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFieldValueReader"/> class.
    /// </summary>
    /// <param name="fieldType">The field type.</param>
    /// <param name="path">The property that holds the value, dotted for a nested one (<c>Address.CountryCode</c>).</param>
    /// <param name="options">The values the settings editor offers, if the field has a fixed list.</param>
    public JsonFieldValueReader(string fieldType, string path, Func<ContentPartFieldDefinition, IEnumerable<ContentConditionOption>> options = null)
    {
        FieldType = fieldType;
        _path = path.Split('.');
        _options = options;
    }

    public string FieldType { get; }

    public Task<ConditionValue> ReadAsync(JsonNode field, ContentPartFieldDefinition definition)
    {
        var node = field;

        foreach (var name in _path)
        {
            node = (node as JsonObject)?[name];
        }

        return Task.FromResult(new ConditionValue(ToStrings(node)));
    }

    public Task DescribeAsync(ContentPartFieldDefinition definition, ContentConditionSourceDescriptor descriptor)
    {
        foreach (var option in _options?.Invoke(definition) ?? [])
        {
            descriptor.Options.Add(option);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a stored JSON value as the invariant strings a condition compares.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The strings; none for <see langword="null"/>.</returns>
    public static IEnumerable<string> ToStrings(JsonNode node)
    {
        switch (node)
        {
            case null:
                yield break;
            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var value in ToStrings(item))
                    {
                        yield return value;
                    }
                }

                yield break;
            case JsonValue value:
                switch (value.GetValueKind())
                {
                    case JsonValueKind.True:
                        yield return "true";
                        break;
                    case JsonValueKind.False:
                        yield return "false";
                        break;
                    case JsonValueKind.Number:
                        yield return value.TryGetValue<decimal>(out var number)
                            ? number.ToString(CultureInfo.InvariantCulture)
                            : value.ToJsonString();
                        break;
                    case JsonValueKind.String:
                        yield return value.GetValue<string>();
                        break;
                }

                yield break;
        }
    }

    /// <summary>
    /// Reads the options a field's settings list (<c>{ "Name": ..., "Value": ... }</c> items).
    /// </summary>
    /// <param name="definition">The field.</param>
    /// <param name="settings">The settings object's name.</param>
    /// <returns>The options.</returns>
    public static IEnumerable<ContentConditionOption> SettingsOptions(ContentPartFieldDefinition definition, string settings)
    {
        if (definition.Settings?[settings]?["Options"] is not JsonArray options)
        {
            yield break;
        }

        foreach (var option in options.OfType<JsonObject>())
        {
            var value = option["Value"]?.GetValue<string>();

            if (value is not null)
            {
                yield return new ContentConditionOption(value, option["Name"]?.GetValue<string>() ?? value);
            }
        }
    }
}
