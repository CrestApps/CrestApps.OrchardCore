using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Services;

namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// Turns the value a property is mapped to into the JSON-LD its kind expects, and writes it to a node.
/// </summary>
internal static class SchemaOrgValueWriter
{
    /// <summary>
    /// Turns a value into the JSON-LD a property expects: a date in ISO 8601, a number, an absolute URL, a name
    /// wrapped in the property's object type, and so on. Text that is a JSON object or array is parsed first.
    /// </summary>
    /// <param name="value">The value, as text or JSON.</param>
    /// <param name="property">The property, or <see langword="null"/> when the vocabulary does not list it.</param>
    /// <param name="context">The structured data context, which turns relative URLs into absolute ones.</param>
    /// <returns>The JSON-LD value, or <see langword="null"/> when there is nothing to write.</returns>
    public static JsonNode Convert(JsonNode value, SchemaOrgProperty property, StructuredDataContext context)
    {
        value = ParseJsonText(value);

        if (IsEmpty(value))
        {
            return null;
        }

        if (property is null)
        {
            return value.DeepClone();
        }

        switch (property.Kind)
        {
            case SchemaOrgValueKind.Json:
                return value.DeepClone();
            case SchemaOrgValueKind.Object:
                return ToObjects(value, property.ObjectType);
            case SchemaOrgValueKind.TextList:
                return ToTextList(value);
            case SchemaOrgValueKind.Image:
                return ToUrls(value, context, allowMany: true);
            case SchemaOrgValueKind.Url:
                return ToUrls(value, context, allowMany: false);
        }

        // The other kinds hold one value, so a list gives its first, or its texts joined.
        if (value is JsonArray array)
        {
            if (property.Kind == SchemaOrgValueKind.Text || property.Kind == SchemaOrgValueKind.LongText)
            {
                var texts = array.Select(GetText).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();

                return texts.Length == 0 ? null : JsonValue.Create(string.Join(", ", texts));
            }

            value = array.FirstOrDefault(item => !IsEmpty(item));

            if (value is null)
            {
                return null;
            }
        }

        var text = GetText(value);

        return property.Kind switch
        {
            SchemaOrgValueKind.LongText => Create(StructuredDataText.ToPlainText(text)),
            SchemaOrgValueKind.Date => FormatDate(value, text, dateOnly: true),
            SchemaOrgValueKind.DateTime => FormatDate(value, text, dateOnly: false),
            SchemaOrgValueKind.Number => ToNumber(value, text),
            SchemaOrgValueKind.Boolean => ToBoolean(value, text),
            SchemaOrgValueKind.Enumeration => Create(ToOption(text, property.Options)),
            _ => Create(text?.Trim()),
        };
    }

    /// <summary>
    /// Writes a value to a node. A dotted name, such as <c>offers.price</c>, writes to a nested object, which is
    /// created with the type <paramref name="getObjectType"/> gives it when it is missing.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    /// <param name="getObjectType">Gives the schema.org type of the nested object at a dotted path, if it has one.</param>
    public static void Set(JsonObject node, string name, JsonNode value, Func<string, string> getObjectType = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (string.IsNullOrWhiteSpace(name) || value is null)
        {
            return;
        }

        var segments = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return;
        }

        var target = node;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (target[segments[i]] is not JsonObject child)
            {
                child = new JsonObject();

                var type = getObjectType?.Invoke(string.Join('.', segments, 0, i + 1));

                if (!string.IsNullOrEmpty(type))
                {
                    child[StructuredDataConstants.Keywords.Type] = type;
                }

                target[segments[i]] = child;
            }

            target = child;
        }

        var last = segments[^1];

        // An object mapped onto an object, such as a brand onto the brand a module wrote, adds to it.
        if (value is JsonObject incoming && target[last] is JsonObject existing)
        {
            JsonLd.Merge(existing, incoming);

            return;
        }

        target[last] = value;
    }

    /// <summary>
    /// Checks that a property name is letters and digits, in segments separated by dots, such as
    /// <c>offers.price</c>.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> when the name is valid.</returns>
    public static bool IsValidPropertyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var segment in name.Split('.'))
        {
            if (segment.Length == 0 || !char.IsLetter(segment[0]) || !segment.All(char.IsLetterOrDigit))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks that a schema.org type name is one word of letters and digits that starts with a letter.
    /// </summary>
    /// <param name="name">The type name.</param>
    /// <returns><see langword="true"/> when the name is valid.</returns>
    public static bool IsValidTypeName(string name)
        => !string.IsNullOrEmpty(name) && char.IsLetter(name[0]) && name.All(char.IsLetterOrDigit);

    private static JsonNode ParseJsonText(JsonNode value)
    {
        if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var text))
        {
            return value;
        }

        text = text.Trim();

        if ((text.StartsWith('{') && text.EndsWith('}')) || (text.StartsWith('[') && text.EndsWith(']')))
        {
            try
            {
                return JsonNode.Parse(text) ?? value;
            }
            catch (JsonException)
            {
                // The value only looks like JSON, so it is kept as text.
            }
        }

        return value;
    }

    private static JsonNode ToObjects(JsonNode value, string objectType)
    {
        if (value is JsonArray array)
        {
            var objects = new JsonArray();

            foreach (var item in array)
            {
                if (ToObjects(item, objectType) is JsonNode converted)
                {
                    objects.Add(converted);
                }
            }

            return objects.Count switch
            {
                0 => null,
                1 => objects[0].DeepClone(),
                _ => objects,
            };
        }

        if (value is JsonObject obj)
        {
            var copy = (JsonObject)obj.DeepClone();

            // A reference to another node, such as the site's publisher, keeps that node's type.
            if (!string.IsNullOrEmpty(objectType) &&
                JsonLd.GetType(copy) is null &&
                !(copy.Count == 1 && copy.ContainsKey(StructuredDataConstants.Keywords.Id)))
            {
                copy[StructuredDataConstants.Keywords.Type] = objectType;
            }

            return copy;
        }

        var text = GetText(value)?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return new JsonObject
        {
            [StructuredDataConstants.Keywords.Type] = string.IsNullOrEmpty(objectType) ? "Thing" : objectType,
            ["name"] = text,
        };
    }

    private static JsonArray ToTextList(JsonNode value)
    {
        var texts = new JsonArray();

        if (value is JsonArray array)
        {
            foreach (var item in array)
            {
                var text = GetText(item is JsonObject obj ? obj["name"] : item)?.Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    texts.Add(text);
                }
            }
        }
        else
        {
            foreach (var line in (GetText(value) ?? string.Empty).Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    texts.Add(line.Trim());
                }
            }
        }

        return texts.Count == 0 ? null : texts;
    }

    private static JsonNode ToUrls(JsonNode value, StructuredDataContext context, bool allowMany)
    {
        var urls = new List<string>();

        foreach (var item in value is JsonArray array ? array : [value])
        {
            var text = GetText(item is JsonObject obj ? obj["url"] : item);
            var url = context?.ToAbsoluteUrl(text) ?? text?.Trim();

            if (!string.IsNullOrEmpty(url))
            {
                urls.Add(url);
            }
        }

        if (urls.Count == 0)
        {
            return null;
        }

        if (!allowMany || urls.Count == 1)
        {
            return JsonValue.Create(urls[0]);
        }

        return new JsonArray([.. urls.Select(url => (JsonNode)JsonValue.Create(url))]);
    }

    private static JsonNode FormatDate(JsonNode value, string text, bool dateOnly)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            return value.DeepClone();
        }

        if (dateOnly)
        {
            return JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        // A time with no time zone, such as one an editor typed, is written as it is.
        return JsonValue.Create(date.Kind switch
        {
            DateTimeKind.Utc => date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateTimeKind.Local => date.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            _ => date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        });
    }

    private static JsonNode ToNumber(JsonNode value, string text)
    {
        if (value is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.Number)
        {
            return value.DeepClone();
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? JsonValue.Create(number)
            : Create(text?.Trim());
    }

    private static JsonNode ToBoolean(JsonNode value, string text)
    {
        if (value is JsonValue jsonValue && jsonValue.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            return value.DeepClone();
        }

        return bool.TryParse(text?.Trim(), out var flag) ? JsonValue.Create(flag) : null;
    }

    /// <summary>
    /// Finds the enumeration value a text means: the value itself, its label, or its name without the schema.org
    /// address, such as <c>InStock</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The values of the enumeration.</param>
    /// <returns>The value.</returns>
    internal static string ToOption(string text, SchemaOrgOption[] options)
    {
        text = text?.Trim();

        if (string.IsNullOrEmpty(text) || options is null)
        {
            return text;
        }

        foreach (var option in options)
        {
            if (string.Equals(option.Value, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(option.Text, text, StringComparison.OrdinalIgnoreCase) ||
                option.Value.EndsWith("/" + text, StringComparison.OrdinalIgnoreCase))
            {
                return option.Value;
            }
        }

        return text;
    }

    private static string GetText(JsonNode node)
        => node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value => value.ToJsonString(),
            JsonObject obj => GetText(obj["name"]),
            _ => null,
        };

    private static JsonValue Create(string text)
        => string.IsNullOrWhiteSpace(text) ? null : JsonValue.Create(text);

    private static bool IsEmpty(JsonNode value)
        => value switch
        {
            null => true,
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => string.IsNullOrWhiteSpace(text),
            JsonArray array => array.Count == 0,
            JsonObject obj => obj.Count == 0,
            _ => false,
        };
}
