namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public interface IOpenAIChatFunctionObjectProperty : IOpenAIChatFunctionProperty
{
    Dictionary<string, IOpenAIChatFunctionProperty> Properties { get; }

    IEnumerable<string> Required { get; }
}
