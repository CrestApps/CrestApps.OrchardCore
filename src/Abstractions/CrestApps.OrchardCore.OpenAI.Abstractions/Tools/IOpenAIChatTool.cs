using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Tools;

public interface IOpenAIChatTool
{
    string Type { get; }

    IOpenAIChatFunction Function { get; }
}
