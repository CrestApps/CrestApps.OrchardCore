
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Functions;

public sealed class EnumFunctionProperty<TEnum> : IOpenAIChatFunctionEnumProperty
    where TEnum : struct, Enum
{
    public OpenAIChatFunctionPropertyType Type => OpenAIChatFunctionPropertyType.String;

    public string Description { get; set; }

    [JsonIgnore]
    public bool IsRequired { get; set; }

    public object DefaultValue { get; set; }

    [JsonPropertyName("enum")]
    public string[] Values { get; }

    public EnumFunctionProperty()
    {
        Values = Enum.GetNames<TEnum>();
    }
}
