
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public sealed class EnumToolProperty<TEnum> : IOpenAIChatFunctionEnumProperty
    where TEnum : struct, Enum
{
    public OpenAIChatFunctionPropertyType Type => OpenAIChatFunctionPropertyType.Array;

    public string Description { get; set; }

    [JsonIgnore]
    public bool IsRequired { get; set; }

    public object DefaultValue { get; set; }

    public EnumItemFunctionProperty Items => new EnumItemToolProperty<TEnum>();
}
