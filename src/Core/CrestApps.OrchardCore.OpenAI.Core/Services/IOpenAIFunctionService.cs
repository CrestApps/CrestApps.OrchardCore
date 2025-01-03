using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public interface IOpenAIFunctionService
{
    IOpenAIChatFunction FindByName(string name);

    IEnumerable<IOpenAIChatFunction> GetFunctions();
}
