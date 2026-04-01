using CrestApps.AI.Completions;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;

namespace CrestApps.AI.Handlers;

/// <summary>
/// Core orchestration context handler that builds the <see cref="AICompletionContext"/>
/// from the resource using the existing <see cref="IAICompletionContextBuilder"/> pipeline,
/// and resolves the <see cref="OrchestrationContext.SourceName"/>.
/// </summary>
internal sealed class CompletionContextOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly IAICompletionContextBuilder _completionContextBuilder;

    public CompletionContextOrchestrationHandler(IAICompletionContextBuilder completionContextBuilder)
    {
        _completionContextBuilder = completionContextBuilder;
    }

    public async Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        // Build the AICompletionContext using the existing handler pipeline.
        var completionContext = await _completionContextBuilder.BuildAsync(context.Resource);
        context.Context.CompletionContext = completionContext;

        // Resolve the SourceName from the connection configured on the completion context.
        if (string.IsNullOrEmpty(context.Context.SourceName)
            && !string.IsNullOrEmpty(completionContext.ConnectionName))
        {
            context.Context.SourceName = completionContext.ConnectionName;
        }

        // Propagate DisableTools from the completion context.
        context.Context.DisableTools = context.Context.CompletionContext.DisableTools;

        // Seed the SystemMessageBuilder with the initial system message.
        if (!string.IsNullOrEmpty(context.Context.CompletionContext.SystemMessage))
        {
            context.Context.SystemMessageBuilder.Append(context.Context.CompletionContext.SystemMessage);
        }
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
        => Task.CompletedTask;
}
