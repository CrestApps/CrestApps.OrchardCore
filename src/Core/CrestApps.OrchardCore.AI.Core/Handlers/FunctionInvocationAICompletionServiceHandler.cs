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
            !(context?.Profile.TryGet<AIProfileFunctionInvocationMetadata>(out var funcMetadata) ?? false))
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        if (funcMetadata.Names is not null && funcMetadata.Names.Length > 0)
        {
            foreach (var name in funcMetadata.Names)
            {
                var tool = await _toolsService.GetByNameAsync(name);

                if (tool is null)
                {
                    continue;
                }

                context.ChatOptions.Tools.Add(tool);
            }
        }

        if (funcMetadata.InstanceIds is not null && funcMetadata.InstanceIds.Length > 0)
        {
            foreach (var instanceId in funcMetadata.InstanceIds)
            {
                var tool = await _toolsService.GetByInstanceIdAsync(instanceId);

                if (tool is null)
                {
                    continue;
                }

                context.ChatOptions.Tools.Add(tool);
            }
        }
    }
}
