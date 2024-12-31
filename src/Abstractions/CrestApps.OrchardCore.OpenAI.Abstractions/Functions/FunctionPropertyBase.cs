using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Functions;

public abstract class FunctionPropertyBase : IOpenAIChatFunctionProperty
{
    public OpenAIChatFunctionPropertyType Type { get; private set; }

    public string Description { get; set; }

    public object DefaultValue { get; set; }

    [JsonIgnore]
    public bool IsRequired { get; set; }

    protected FunctionPropertyBase(OpenAIChatFunctionPropertyType type)
    {
        Type = type;
    }
}
