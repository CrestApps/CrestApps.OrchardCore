using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Json;

public class AIConnectionEntryConverter : JsonConverter<AIConnectionEntry>
{
    public override AIConnectionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var entry = new AIConnectionEntry();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();

            reader.Read();

            if (propertyName == nameof(AIConnectionEntry.Name))
            {
                entry.Name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }

            var property = JNode.Load(ref reader);
            entry.Data[propertyName] = property;
        }

        return entry;
    }

    public override void Write(Utf8JsonWriter writer, AIConnectionEntry value, JsonSerializerOptions options)
    {
        var o = new JsonObject()
        {
            // Write all well-known properties.
            [nameof(AIConnectionEntry.Name)] = value.Name,
            [nameof(AIConnectionEntry.Data)] = value.Data,
        };

        // Write all custom content properties.
        o.Merge(value.Data);

        o.WriteTo(writer);
    }
}
