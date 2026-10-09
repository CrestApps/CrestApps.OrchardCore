using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reports.Contents;

/// <summary>
/// Reads report values from the JSON of a content item. A content part is stored at
/// <c>contentItem.Content[partName]</c> and a content field at <c>contentItem.Content[partName][fieldName]</c>.
/// </summary>
public static class ContentReportJson
{
    /// <summary>
    /// The separator <see cref="ContentReportValueMode.Join"/> places between array entries.
    /// </summary>
    public const string JoinSeparator = ",";

    /// <summary>
    /// Gets the JSON object of a content part, or of a content field when <paramref name="fieldName"/> is given.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="partName">The name of the part in the content type, which is also its JSON property name.</param>
    /// <param name="fieldName">The optional name of the field in the part.</param>
    /// <returns>The JSON object, or <see langword="null"/> when the content item does not have it.</returns>
    public static JsonObject GetElement(ContentItem contentItem, string partName, string fieldName = null)
    {
        if (contentItem is null || string.IsNullOrEmpty(partName))
        {
            return null;
        }

        var content = (JsonObject)contentItem.Content;

        if (content?[partName] is not JsonObject part)
        {
            return null;
        }

        if (string.IsNullOrEmpty(fieldName))
        {
            return part;
        }

        return part[fieldName] as JsonObject;
    }

    /// <summary>
    /// Reads a property of a JSON object.
    /// </summary>
    /// <param name="element">The JSON object of a part or field.</param>
    /// <param name="mode">How the property becomes a value.</param>
    /// <param name="propertyNames">The property names to try, in order. The first one present with a value wins.</param>
    /// <returns>
    /// The raw value, which <see cref="ReportDataValues.Coerce(object, ReportDataType)"/> can convert, or
    /// <see langword="null"/> when no property has a value.
    /// </returns>
    public static object ReadProperty(JsonObject element, ContentReportValueMode mode, params string[] propertyNames)
    {
        if (element is null || propertyNames is null)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (string.IsNullOrEmpty(propertyName) ||
                !element.TryGetPropertyValue(propertyName, out var node) ||
                node is null)
            {
                continue;
            }

            var value = mode switch
            {
                ContentReportValueMode.First => ReadStrings(node).FirstOrDefault(),
                ContentReportValueMode.Join => Join(ReadStrings(node)),
                _ => ReadValue(node),
            };

            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a JSON node to a value <see cref="ReportDataValues.Coerce(object, ReportDataType)"/> can convert.
    /// </summary>
    /// <param name="node">The JSON node.</param>
    /// <returns>A <see cref="JsonElement"/> for a JSON value, the node itself for an object or array, or
    /// <see langword="null"/> for a missing or JSON null value.</returns>
    public static object ReadValue(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return node;
        }

        if (!value.TryGetValue<JsonElement>(out var element))
        {
            element = value.Deserialize<JsonElement>();
        }

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : element;
    }

    /// <summary>
    /// Reads the non-empty text entries of a JSON array, or the text of a single JSON value.
    /// </summary>
    /// <param name="node">The JSON node.</param>
    /// <returns>The text entries, in order.</returns>
    public static IEnumerable<string> ReadStrings(JsonNode node)
    {
        if (node is JsonArray array)
        {
            foreach (var entry in array)
            {
                var text = ReportDataValues.ToText(ReadValue(entry));

                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }

            yield break;
        }

        if (node is JsonValue)
        {
            var text = ReportDataValues.ToText(ReadValue(node));

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }
        }
    }

    /// <summary>
    /// Joins text entries with <see cref="JoinSeparator"/>.
    /// </summary>
    /// <param name="values">The entries.</param>
    /// <returns>The joined text, or <see langword="null"/> when there are no entries.</returns>
    public static string Join(IEnumerable<string> values)
    {
        if (values is null)
        {
            return null;
        }

        var joined = string.Join(JoinSeparator, values.Where(value => !string.IsNullOrWhiteSpace(value)));

        return joined.Length == 0
            ? null
            : joined;
    }
}
