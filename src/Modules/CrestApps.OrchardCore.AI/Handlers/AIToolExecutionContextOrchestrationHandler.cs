using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Sets the <see cref="AIToolExecutionContext"/> on the current <see cref="AIInvocationScope"/>
/// after the orchestration context is fully built. This removes the need for individual
/// hubs (AIChatHub, ChatInteractionHub) to manually construct and store the context.
/// </summary>
internal sealed class AIToolExecutionContextOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        var invocationContext = AIInvocationScope.Current;

        if (invocationContext is null)
        {
            return Task.CompletedTask;
        }

        invocationContext.ToolExecutionContext ??= new AIToolExecutionContext(context.Resource);

        invocationContext.ToolExecutionContext.ProviderName = context.OrchestrationContext.SourceName;
        invocationContext.ToolExecutionContext.ConnectionName = context.OrchestrationContext.CompletionContext?.ConnectionName;

        return Task.CompletedTask;
    }
}
