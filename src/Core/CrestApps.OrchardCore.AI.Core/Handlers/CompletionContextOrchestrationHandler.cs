using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Core orchestration context handler that builds the <see cref="AICompletionContext"/>
/// from the resource using the existing <see cref="IAICompletionContextBuilder"/> pipeline,
/// and resolves the <see cref="OrchestrationContext.SourceName"/>.
/// </summary>
internal sealed class CompletionContextOrchestrationHandler : IOrchestrationContextHandler
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
        context.Context.CompletionContext.UserMarkdownInResponse = true;

        // Resolve SourceName from the resource (AIProfile or ChatInteraction).
        context.Context.SourceName = ResolveSourceName(context.Resource);
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
        => Task.CompletedTask;

    private static string ResolveSourceName(object resource)
    {
        if (resource is AIProfile profile)
        {
            return profile.Source;
        }

        if (resource is ChatInteraction interaction)
        {
            return interaction.Source;
        }

        return null;
    }
}
