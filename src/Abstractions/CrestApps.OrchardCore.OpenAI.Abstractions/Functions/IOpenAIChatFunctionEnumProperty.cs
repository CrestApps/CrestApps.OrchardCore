namespace CrestApps.OrchardCore.OpenAI.Functions;

public interface IOpenAIChatFunctionEnumProperty : IOpenAIChatFunctionProperty
{
    string[] Values { get; }
}
