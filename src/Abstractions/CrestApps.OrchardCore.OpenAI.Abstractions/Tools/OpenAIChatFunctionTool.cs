using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Tools;

public class OpenAIChatFunctionTool : IOpenAIChatTool
{
    public const string FunctionType = "function";

    public string Type => FunctionType;

    public IOpenAIChatFunction Function { get; }

    public OpenAIChatFunctionTool(IOpenAIChatFunction function)
    {
        Function = function;
    }
}
