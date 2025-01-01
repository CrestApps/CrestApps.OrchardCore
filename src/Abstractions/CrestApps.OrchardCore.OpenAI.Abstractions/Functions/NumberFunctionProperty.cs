namespace CrestApps.OrchardCore.OpenAI.Functions;

public sealed class NumberFunctionProperty : FunctionPropertyBase, IOpenAIChatFunctionFormattedProperty
{
    public NumberFunctionProperty()
        : base(OpenAIChatFunctionPropertyType.Number)
    {
    }

    public NumberFunctionProperty(string format)
        : base(OpenAIChatFunctionPropertyType.Number)
    {
        Format = format;
    }

    public string Format { get; }

    public static NumberFunctionProperty Integer => new("int32");

    public static NumberFunctionProperty Long => new("int64");

    public static NumberFunctionProperty Float => new("float");

    public static NumberFunctionProperty Decimal => new("decimal");
}
