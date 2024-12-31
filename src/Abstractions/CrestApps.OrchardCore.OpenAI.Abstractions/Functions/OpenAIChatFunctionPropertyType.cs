using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Functions;

[JsonConverter(typeof(CustomEnumConverter<OpenAIChatFunctionPropertyType>))]
public enum OpenAIChatFunctionPropertyType
{
    String,
    Number,
    Boolean,
    Array,
    Object,
}

public sealed class CustomEnumConverter<T> : JsonConverter<T> where T : Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = reader.GetString();
        foreach (var field in typeToConvert.GetFields())
        {
            var enumMemberAttribute = field.GetCustomAttribute<EnumMemberAttribute>();
            if (enumMemberAttribute != null && enumMemberAttribute.Value == value)
            {
                return (T)field.GetValue(null);
            }
        }

        // If no match is found, fall back to default enum parsing
        return (T)Enum.Parse(typeToConvert, value, true);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var enumMember = value.GetType().GetField(value.ToString())
            .GetCustomAttribute<EnumMemberAttribute>();

        // If EnumMember attribute exists, use its value, otherwise use the enum name
        if (enumMember != null)
        {
            writer.WriteStringValue(enumMember.Value);
        }
        else
        {
            writer.WriteStringValue(value.ToString().ToLowerInvariant());
        }
    }
}
