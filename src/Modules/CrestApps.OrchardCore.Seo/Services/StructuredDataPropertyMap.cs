using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Reads and applies the <c>property: value</c> lines of a content type's schema.org property map.
/// </summary>
internal static class StructuredDataPropertyMap
{
    /// <summary>
    /// Parses the lines of a property map. Empty lines and lines that start with <c>#</c> are skipped, and the name
    /// ends at the first colon so the value can hold colons, such as a Liquid date format.
    /// </summary>
    /// <param name="text">The property map.</param>
    /// <returns>The properties, in the order they are written.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Parse(string text)
    {
        var result = new List<KeyValuePair<string, string>>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            var name = line.Substring(0, separator).Trim();
            var value = line.Substring(separator + 1).Trim();

            if (name.Length == 0 || name.Contains(' '))
            {
                continue;
            }

            result.Add(new KeyValuePair<string, string>(name, value));
        }

        return result;
    }

    /// <summary>
    /// Sets a property on a node from its rendered value. A dotted name, such as <c>offers.price</c>, sets a nested
    /// property and creates the objects on the way. A value that is a JSON object or array is stored as JSON; any
    /// other value is stored as text. An empty value leaves the node unchanged.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The rendered value.</param>
    public static void Apply(JsonObject node, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
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
                target[segments[i]] = child;
            }

            target = child;
        }

        target[segments[segments.Length - 1]] = ToNode(value.Trim());
    }

    private static JsonNode ToNode(string value)
    {
        if ((value.StartsWith('{') && value.EndsWith('}')) || (value.StartsWith('[') && value.EndsWith(']')))
        {
            try
            {
                var parsed = JsonNode.Parse(value);

                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // The value only looks like JSON, so it is kept as text.
            }
        }

        return JsonValue.Create(value);
    }
}
