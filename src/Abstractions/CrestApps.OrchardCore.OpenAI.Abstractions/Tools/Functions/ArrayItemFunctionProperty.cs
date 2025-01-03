namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public class ArrayItemFunctionProperty
{
    public OpenAIChatFunctionPropertyType Type { get; }

    public ArrayItemFunctionProperty(OpenAIChatFunctionPropertyType type)
    {
        Type = type;
    }
}
