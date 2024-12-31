namespace CrestApps.OrchardCore.OpenAI.Functions;

public sealed class StringFunctionProperty : FunctionPropertyBase
{
    public StringFunctionProperty()
        : base(OpenAIChatFunctionPropertyType.String)
    {
    }
}
