using CrestApps.OrchardCore.AI.Models;

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
            context.CompletionContext is null ||
            context.CompletionContext.ToolNames is null ||
            context.CompletionContext.ToolNames.Length == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        foreach (var toolName in context.CompletionContext.ToolNames)
        {
            var tool = await _toolsService.GetByNameAsync(toolName);

            if (tool is null)
            {
                continue;
            }

            context.ChatOptions.Tools.Add(tool);
        }
    }
}

