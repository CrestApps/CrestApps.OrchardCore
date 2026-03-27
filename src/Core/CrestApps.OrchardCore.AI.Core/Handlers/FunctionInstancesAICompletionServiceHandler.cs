using CrestApps.OrchardCore.AI.Models;

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
            context.CompletionContext is null ||
            context.CompletionContext.InstanceIds is null ||
            context.CompletionContext.InstanceIds.Length == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        foreach (var instanceId in context.CompletionContext.InstanceIds)
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

