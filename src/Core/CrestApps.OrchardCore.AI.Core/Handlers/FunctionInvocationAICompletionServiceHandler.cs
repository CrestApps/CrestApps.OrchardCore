using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class FunctionInvocationAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IAIToolsService _toolsService;

    public FunctionInvocationAICompletionServiceHandler(IAIToolsService toolsService)
    {
        _toolsService = toolsService;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported ||
            !context.Profile.TryGet<AIProfileFunctionInvocationMetadata>(out var metadata) ||
            metadata.Names is null || metadata.Names.Length == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        foreach (var name in metadata.Names)
        {
            var tool = await _toolsService.GetByNameAsync(name);

            if (tool is null)
            {
                continue;
            }

            context.ChatOptions.Tools.Add(tool);
        }
    }
}

