using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The built-in AI chat response handler that routes prompts through the orchestration pipeline.
/// This is the default handler used when no custom <see cref="IChatResponseHandler"/> is configured.
/// </summary>
internal sealed class AIChatResponseHandler : IChatResponseHandler
{
    /// <summary>
    /// The well-known name for the built-in AI handler.
    /// </summary>
    public const string HandlerName = "AI";

    public string Name => HandlerName;

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var orchestrationContextBuilder = context.Services.GetRequiredService<IOrchestrationContextBuilder>();
        var orchestratorResolver = context.Services.GetRequiredService<IOrchestratorResolver>();

        OrchestrationContext orchestratorContext;
        string orchestratorName;

        if (context.ChatType == ChatContextType.AIChatSession)
        {
            orchestratorContext = await orchestrationContextBuilder.BuildAsync(context.Profile, ctx =>
            {
                ctx.UserMessage = context.Prompt;
                ctx.ConversationHistory = context.ConversationHistory;
                ctx.CompletionContext.AdditionalProperties["Session"] = context.ChatSession;
            });

            orchestratorName = context.Profile.OrchestratorName;

            // Store the session in the invocation context so document tools can resolve session documents.
            AIInvocationScope.Current.Items[nameof(AIChatSession)] = context.ChatSession;
            AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;
        }
        else
        {
            orchestratorContext = await orchestrationContextBuilder.BuildAsync(context.Interaction, ctx =>
            {
                ctx.UserMessage = context.Prompt;
                ctx.ConversationHistory = context.ConversationHistory;
            });

            orchestratorName = context.Interaction.OrchestratorName;

            AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;
        }

        // Store the orchestration context in Properties so the hub can access it for citation collection.
        context.Properties["OrchestrationContext"] = orchestratorContext;

        var orchestrator = orchestratorResolver.Resolve(orchestratorName);
        var stream = orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken);

        return ChatResponseHandlerResult.Streaming(stream);
    }
}
