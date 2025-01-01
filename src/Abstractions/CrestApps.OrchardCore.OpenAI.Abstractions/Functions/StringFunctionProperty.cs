namespace CrestApps.OrchardCore.OpenAI.Functions;

public class StringFunctionProperty : FunctionPropertyBase, IOpenAIChatFunctionFormattedProperty
{
    public StringFunctionProperty()
        : base(OpenAIChatFunctionPropertyType.String)
    {
    }

    public StringFunctionProperty(string format)
    : base(OpenAIChatFunctionPropertyType.String)
    {
        Format = format;
    }

    public string Format { get; }

    public static StringFunctionProperty DateTime => new("date-time");

    public static StringFunctionProperty Uri => new("uri");

    public static StringFunctionProperty Hostname => new("hostname");

    public static StringFunctionProperty Ipv4 => new("ipv4");

    public static StringFunctionProperty Ipv6 => new("ipv6");

    public static StringFunctionProperty UUID => new("uuid");

    public static StringFunctionProperty Phone => new("phone");

    public static StringFunctionProperty CreditCard => new("credit-card");

    public static StringFunctionProperty Password => new("password");
}
