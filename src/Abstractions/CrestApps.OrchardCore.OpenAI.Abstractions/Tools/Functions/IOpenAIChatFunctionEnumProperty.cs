namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public interface IOpenAIChatFunctionEnumProperty : IOpenAIChatFunctionProperty
{
    EnumItemFunctionProperty Items { get; }
}
