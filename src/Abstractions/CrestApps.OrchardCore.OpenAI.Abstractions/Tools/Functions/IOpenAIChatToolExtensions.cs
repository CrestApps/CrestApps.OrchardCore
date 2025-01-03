namespace CrestApps.OrchardCore.OpenAI.Tools.Functions;

public static class IOpenAIChatToolExtensions
{
    public static bool IsFunction(this IOpenAIChatTool tool)
    {
        return tool.Type == OpenAIChatFunctionTool.FunctionType && tool.Function is not null;
    }
}
