namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public interface IOpenAIChatFunctionFormattedProperty : IOpenAIChatFunctionProperty
{
    string Format { get; }
}
