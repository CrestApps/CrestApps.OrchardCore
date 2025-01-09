namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public class StringFunctionProperty : FunctionPropertyBase, IOpenAIChatFunctionFormattedProperty
{
    public string Format { get; }

    public StringFunctionProperty()
        : base(OpenAIChatFunctionPropertyType.String)
    {
    }

    public StringFunctionProperty(string format)
        : base(OpenAIChatFunctionPropertyType.String)
    {
        Format = format;
    }

    public StringFunctionProperty(string format, string description, bool isRequired = false, object defaultValue = null)
        : base(OpenAIChatFunctionPropertyType.String)
    {
        Format = format;
        Description = description;
        IsRequired = isRequired;
        DefaultValue = defaultValue;
    }

    public static StringFunctionProperty DateTime(string description, bool isRequired = false, object defaultValue = null)
        => new("date-time", description, isRequired, defaultValue);

    public static StringFunctionProperty Uri(string description, bool isRequired = false, object defaultValue = null)
        => new("uri", description, isRequired, defaultValue);

    public static StringFunctionProperty Hostname(string description, bool isRequired = false, object defaultValue = null)
        => new("hostname", description, isRequired, defaultValue);

    public static StringFunctionProperty Ipv4(string description, bool isRequired = false, object defaultValue = null)
        => new("ipv4", description, isRequired, defaultValue);

    public static StringFunctionProperty Ipv6(string description, bool isRequired = false, object defaultValue = null)
        => new("ipv6", description, isRequired, defaultValue);

    public static StringFunctionProperty UUID(string description, bool isRequired = false, object defaultValue = null)
        => new("uuid", description, isRequired, defaultValue);

    public static StringFunctionProperty Phone(string description, bool isRequired = false, object defaultValue = null)
        => new("phone", description, isRequired, defaultValue);

    public static StringFunctionProperty CreditCard(string description, bool isRequired = false, object defaultValue = null)
        => new("credit-card", description, isRequired, defaultValue);

    public static StringFunctionProperty Password(string description, bool isRequired = false, object defaultValue = null)
        => new("password", description, isRequired, defaultValue);
}
