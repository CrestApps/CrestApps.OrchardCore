using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Core;

public static class OpenAIFunctionServiceExtensions
{
    public static IEnumerable<IOpenAIChatFunction> FindByNames(this IOpenAIFunctionService service, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var function = service.FindByName(name);

            if (function != null)
            {
                yield return function;
            }
        }
    }
}
