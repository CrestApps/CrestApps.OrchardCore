using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class FunctionInstancesAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IAIToolsService _toolsService;

    public FunctionInstancesAICompletionServiceHandler(IAIToolsService toolsService)
    {
        _toolsService = toolsService;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported ||
            !context.Profile.TryGet<AIProfileFunctionInstancesMetadata>(out var metadata) ||
            metadata.InstanceIds is null ||
            metadata.InstanceIds.Length == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        foreach (var instanceId in metadata.InstanceIds)
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

