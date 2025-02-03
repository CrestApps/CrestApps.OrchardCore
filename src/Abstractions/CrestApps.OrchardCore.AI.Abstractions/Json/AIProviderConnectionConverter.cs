using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Json;

public sealed class AIProviderConnectionConverter : JsonConverter<AIProviderConnection>
{
    public override AIProviderConnection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialize into a dictionary first
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
        return dictionary != null ? new AIProviderConnection(dictionary) : null;
    }

    public override void Write(Utf8JsonWriter writer, AIProviderConnection value, JsonSerializerOptions options)
    {
        // Serialize as dictionary
        JsonSerializer.Serialize(writer, (IDictionary<string, object>)value, options);
    }
}
