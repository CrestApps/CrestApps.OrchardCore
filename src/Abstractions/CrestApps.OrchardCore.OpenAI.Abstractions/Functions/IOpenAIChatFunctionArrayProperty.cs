namespace CrestApps.OrchardCore.OpenAI.Functions;

public interface IOpenAIChatFunctionArrayProperty : IOpenAIChatFunctionProperty
{
    IOpenAIChatFunctionProperty ItemType { get; }
}
