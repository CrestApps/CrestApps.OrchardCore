namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public sealed class NumberFunctionProperty : FunctionPropertyBase, IOpenAIChatFunctionFormattedProperty
{
    public string Format { get; }

    public NumberFunctionProperty()
        : base(OpenAIChatFunctionPropertyType.Number)
    {
    }

    public NumberFunctionProperty(string format)
        : base(OpenAIChatFunctionPropertyType.Number)
    {
        Format = format;
    }

    public NumberFunctionProperty(string format, string description, bool isRequired = false, object defaultValue = null)
        : base(OpenAIChatFunctionPropertyType.Number)
    {
        Format = format;
        Description = description;
        IsRequired = isRequired;
        DefaultValue = defaultValue;
    }

    public static NumberFunctionProperty Integer(string description, bool isRequired = false, object defaultValue = null)
        => new("int32", description, isRequired, defaultValue);

    public static NumberFunctionProperty Long(string description, bool isRequired = false, object defaultValue = null)
        => new("int64", description, isRequired, defaultValue);

    public static NumberFunctionProperty Float(string description, bool isRequired = false, object defaultValue = null)
        => new("float", description, isRequired, defaultValue);

    public static NumberFunctionProperty Decimal(string description, bool isRequired = false, object defaultValue = null)
        => new("decimal", description, isRequired, defaultValue);
}
