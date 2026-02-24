using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Json;

public sealed class AIProviderConnectionConverter : JsonConverter<AIProviderConnectionEntry>
{
    /// <summary>
    /// Maps legacy configuration key names to their current equivalents.
    /// </summary>
    private static readonly Dictionary<string, string> _legacyKeyMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DefaultDeploymentName"] = "ChatDeploymentName",
        ["DefaultChatDeploymentName"] = "ChatDeploymentName",
        ["DefaultUtilityDeploymentName"] = "UtilityDeploymentName",
        ["DefaultEmbeddingDeploymentName"] = "EmbeddingDeploymentName",
        ["DefaultImagesDeploymentName"] = "ImagesDeploymentName",
    };

    public override AIProviderConnectionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialize into a dictionary first.
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);

        if (dictionary is null)
        {
            return null;
        }

        // Migrate legacy keys to current keys.
        foreach (var (legacyKey, newKey) in _legacyKeyMappings)
        {
            if (dictionary.TryGetValue(legacyKey, out var value) && !dictionary.ContainsKey(newKey))
            {
                dictionary[newKey] = value;
                dictionary.Remove(legacyKey);
            }
        }

        return new AIProviderConnectionEntry(dictionary);
    }

    public override void Write(Utf8JsonWriter writer, AIProviderConnectionEntry value, JsonSerializerOptions options)
    {
        // Serialize as dictionary.
        JsonSerializer.Serialize(writer, (IDictionary<string, object>)value, options);
    }
}
