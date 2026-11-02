using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Helpers that create, read, merge and write JSON-LD nodes.
/// </summary>
public static class JsonLd
{
    private static readonly JsonSerializerOptions _scriptOptions = new()
    {
        // The encoder escapes <, >, & and quotes, so the JSON cannot close the script element it is written in.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private static readonly JsonSerializerOptions _indentedScriptOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    /// <summary>
    /// Creates a node of a schema.org type.
    /// </summary>
    /// <param name="type">The schema.org type, such as <c>Product</c>.</param>
    /// <param name="id">The optional node identifier, usually an absolute URL with a fragment.</param>
    /// <returns>The node.</returns>
    public static JsonObject CreateNode(string type, string id = null)
    {
        var node = new JsonObject();

        if (!string.IsNullOrEmpty(type))
        {
            node[StructuredDataConstants.Keywords.Type] = type;
        }

        if (!string.IsNullOrEmpty(id))
        {
            node[StructuredDataConstants.Keywords.Id] = id;
        }

        return node;
    }

    /// <summary>
    /// Creates a reference to another node of the graph: <c>{ "@id": "..." }</c>.
    /// </summary>
    /// <param name="id">The identifier of the node.</param>
    /// <returns>The reference.</returns>
    public static JsonObject Reference(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return new JsonObject
        {
            [StructuredDataConstants.Keywords.Id] = id,
        };
    }

    /// <summary>
    /// Gets the schema.org type of a node. When the node has several types, the first one is returned.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The type, or <see langword="null"/> when the node has none.</returns>
    public static string GetType(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var value = node[StructuredDataConstants.Keywords.Type];

        if (value is JsonArray types)
        {
            foreach (var type in types)
            {
                if (type is JsonValue typeValue && typeValue.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        return GetString(node, StructuredDataConstants.Keywords.Type);
    }

    /// <summary>
    /// Gets a string property of a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The value, or <see langword="null"/> when the property is missing or is not a string.</returns>
    public static string GetString(JsonObject node, string name)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text))
        {
            return text;
        }

        return null;
    }

    /// <summary>
    /// Sets a string property when the value is not empty. An empty value leaves the property as it is.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    public static void SetIfNotEmpty(JsonObject node, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!string.IsNullOrWhiteSpace(value))
        {
            node[name] = value.Trim();
        }
    }

    /// <summary>
    /// Sets a property only when the node does not have it yet.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    public static void SetIfMissing(JsonObject node, string name, JsonNode value)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (value is null || node[name] is not null)
        {
            return;
        }

        node[name] = value.Parent is null ? value : value.DeepClone();
    }

    /// <summary>
    /// Merges the properties of <paramref name="source"/> into <paramref name="target"/>. Values in the source
    /// replace the values the target already had.
    /// </summary>
    /// <param name="target">The node that receives the properties.</param>
    /// <param name="source">The node that gives the properties.</param>
    public static void Merge(JsonObject target, JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        foreach (var property in source)
        {
            if (property.Key == StructuredDataConstants.Keywords.Context)
            {
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    /// <summary>
    /// Parses JSON-LD text, such as the JSON an editor typed, into nodes. A document with a <c>@graph</c> gives each
    /// node of the graph, an array gives each object, and an object gives itself.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="nodes">The nodes.</param>
    /// <returns><see langword="true"/> when the text is valid JSON that holds at least one object.</returns>
    public static bool TryParseNodes(string json, out IReadOnlyList<JsonObject> nodes)
    {
        nodes = [];

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        JsonNode root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        var result = new List<JsonObject>();

        if (root is JsonObject obj && obj[StructuredDataConstants.Keywords.Graph] is JsonArray graph)
        {
            AddObjects(graph, result);
        }
        else if (root is JsonObject single)
        {
            result.Add(single);
        }
        else if (root is JsonArray array)
        {
            AddObjects(array, result);
        }

        nodes = result;

        return result.Count > 0;
    }

    /// <summary>
    /// Writes a node as JSON that is safe to place inside a <c>&lt;script type="application/ld+json"&gt;</c>
    /// element.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="indented">Whether to indent the JSON.</param>
    /// <returns>The JSON text.</returns>
    public static string ToScriptJson(JsonNode node, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.ToJsonString(indented ? _indentedScriptOptions : _scriptOptions);
    }

    private static void AddObjects(JsonArray array, List<JsonObject> result)
    {
        foreach (var item in array)
        {
            if (item is JsonObject obj)
            {
                result.Add(obj);
            }
        }
    }
}
