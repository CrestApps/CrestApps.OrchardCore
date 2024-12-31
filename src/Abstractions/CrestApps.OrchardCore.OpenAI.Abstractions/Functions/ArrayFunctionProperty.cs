using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Functions;

public sealed class ArrayFunctionProperty<TType> : IOpenAIChatFunctionArrayProperty
    where TType : IOpenAIChatFunctionProperty, new()
{
    public OpenAIChatFunctionPropertyType Type => OpenAIChatFunctionPropertyType.Array;

    public string Description { get; set; }

    [JsonIgnore]
    public bool IsRequired { get; set; }

    public object DefaultValue { get; set; }

    public IOpenAIChatFunctionProperty ItemType { get; } = new TType();
}
