namespace CrestApps.OrchardCore.OpenAI.Functions;

public interface IOpenAIChatFunctionEnumProperty : IOpenAIChatFunctionProperty
{
    string[] Values { get; }
}

public interface IOpenAIChatFunctionFormattedProperty : IOpenAIChatFunctionProperty
{
    string Format { get; }
}
