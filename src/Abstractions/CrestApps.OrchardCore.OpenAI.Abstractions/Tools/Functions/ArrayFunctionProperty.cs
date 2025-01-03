using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public class ArrayFunctionProperty : IOpenAIChatArrayFunctionProperty
{
    public OpenAIChatFunctionPropertyType Type => OpenAIChatFunctionPropertyType.Array;

    public string Description { get; set; }

    [JsonIgnore]
    public bool IsRequired { get; set; }

    public object DefaultValue { get; set; }

    public ArrayFunctionProperty(OpenAIChatFunctionPropertyType type)
    {
        Items = new ArrayItemFunctionProperty(type);
    }

    public ArrayItemFunctionProperty Items { get; }
}
