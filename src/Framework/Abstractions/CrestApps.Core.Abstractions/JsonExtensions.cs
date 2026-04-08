using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.Core;

/// <summary>
/// Extension methods for JSON types to replace OrchardCore's JSON helpers.
/// </summary>
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates a <see cref="JsonObject"/> from an object.
    /// </summary>
    public static JsonObject FromObject<T>(T value, JsonSerializerOptions options = null)
    {
        if (value is null)
        {
            return [];
        }

        var json = JsonSerializer.Serialize(value, options ?? _defaultOptions);

        return JsonNode.Parse(json)?.AsObject() ?? [];
    }

    /// <summary>
    /// Deep-clones an <see cref="IDictionary{String, Object}"/> used for extensible properties.
    /// </summary>
    public static IDictionary<string, object> Clone(this IDictionary<string, object> properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return new Dictionary<string, object>();
        }

        var json = JsonSerializer.Serialize(properties);

        return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Deep-clones a <see cref="JsonObject"/>.
    /// </summary>
    public static JsonObject Clone(this JsonObject jsonObject)
    {
        if (jsonObject is null)
        {
            return [];
        }

        return jsonObject.DeepClone().AsObject();
    }
}
