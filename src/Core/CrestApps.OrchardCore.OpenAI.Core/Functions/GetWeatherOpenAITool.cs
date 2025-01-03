using CrestApps.OrchardCore.OpenAI.Tools;

namespace CrestApps.OrchardCore.OpenAI.Core.Functions;

public sealed class GetWeatherOpenAITool : OpenAIChatFunctionTool, IOpenAIChatTool
{
    public GetWeatherOpenAITool(GetWeatherOpenAIFunction function)
        : base(function)
    {
    }
}
