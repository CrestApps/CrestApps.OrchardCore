using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Json;

internal sealed class TypeJsonConverter : JsonConverter<Type>
{
    internal static readonly TypeJsonConverter Instance = new();

    public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Type.GetType(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }
}
