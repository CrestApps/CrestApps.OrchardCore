using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Functions;

public sealed class OpenAIChatFunctionPropertyConverter : JsonConverter<IOpenAIChatFunctionProperty>
{
    public static readonly OpenAIChatFunctionPropertyConverter Instance = new();

    public override IOpenAIChatFunctionProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Read the "type" field to determine which concrete class to use
        if (root.TryGetProperty("type", out var typeProperty))
        {
            var typeName = typeProperty.GetString();
            var type = Type.GetType(typeName);

            if (type == null)
            {
                throw new JsonException($"Unable to find type: {typeName}");
            }

            // Dynamically call the correct generic method to deserialize to the specific type
            var deserializeMethod = typeof(JsonSerializer)
                .GetMethod("Deserialize", [typeof(string), typeof(JsonSerializerOptions)])
                .MakeGenericMethod(type);

            // Deserialize into the concrete type based on the "type" field
            return (IOpenAIChatFunctionProperty)deserializeMethod.Invoke(null, [root.GetRawText(), options]);
        }

        throw new JsonException("Missing or invalid 'type' property.");
    }

    public override void Write(Utf8JsonWriter writer, IOpenAIChatFunctionProperty value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
