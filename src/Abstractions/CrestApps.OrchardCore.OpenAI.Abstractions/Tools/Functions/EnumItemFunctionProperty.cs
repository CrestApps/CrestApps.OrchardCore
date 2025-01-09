using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public class EnumItemFunctionProperty : ArrayItemFunctionProperty
{
    [JsonPropertyName("enum")]
    public string[] Values { get; set; }

    public EnumItemFunctionProperty()
        : base(OpenAIChatFunctionPropertyType.String)
    {

    }
}

public sealed class EnumItemToolProperty<TEnum> : EnumItemFunctionProperty
    where TEnum : struct, Enum
{
    public EnumItemToolProperty()
    {
        Values = Enum.GetNames<TEnum>();
    }
}
