using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIFunctionService
{
    IOpenAIChatFunction FindByName(string name);

    IEnumerable<IOpenAIChatFunction> GetFunctions();
}
