using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Json;

public class OpenAIConnectionEntryConverter : JsonConverter<OpenAIConnectionEntry>
{
    public override OpenAIConnectionEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var entry = new OpenAIConnectionEntry();

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

            if (propertyName == nameof(OpenAIConnectionEntry.Name))
            {
                entry.Name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }

            var property = JNode.Load(ref reader);
            entry.Data[propertyName] = property;
        }

        return entry;
    }

    public override void Write(Utf8JsonWriter writer, OpenAIConnectionEntry value, JsonSerializerOptions options)
    {
        var o = new JsonObject()
        {
            // Write all well-known properties.
            [nameof(OpenAIConnectionEntry.Name)] = value.Name,
            [nameof(OpenAIConnectionEntry.Data)] = value.Data,
        };

        // Write all custom content properties.
        o.Merge(value.Data);

        o.WriteTo(writer);
    }
}
