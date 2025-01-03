namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public interface IOpenAIChatArrayFunctionProperty : IOpenAIChatFunctionProperty
{
    ArrayItemFunctionProperty Items { get; }
}
