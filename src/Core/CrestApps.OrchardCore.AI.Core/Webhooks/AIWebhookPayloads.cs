using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;

namespace CrestApps.OrchardCore.AI.Core.Webhooks;

/// <summary>
/// The bodies of the webhook events raised from more than one place.
/// </summary>
public static class AIWebhookPayloads
{
    /// <summary>
    /// The body of <see cref="AIWebhookEvents.ConversationAnalyzed"/>: the conversation, its variables and its analysis
    /// results.
    /// </summary>
    /// <param name="session">The conversation.</param>
    /// <param name="profile">Its profile.</param>
    /// <returns>The body.</returns>
    public static object Analyzed(AIChatSession session, AIProfile profile)
    {
        return new
        {
            sessionId = session?.SessionId,
            profileId = profile?.ItemId ?? session?.ProfileId,
            variables = session?.GetConversationVariables(),
            results = session?.PostSessionResults,
        };
    }
}

/// <summary>
/// Raises <see cref="AIWebhookEvents.ConversationStarted"/> when a conversation is about to start.
/// </summary>
public sealed class AIWebhookConversationStartingHandler : IAIConversationStartingHandler
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhookConversationStartingHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The request's services.</param>
    public AIWebhookConversationStartingHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public Task StartingAsync(AIConversationContext conversation, AIConversationStartingResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        return AIWebhookPublisher.TryPublishAsync(_serviceProvider, AIWebhookEvents.ConversationStarted, new
        {
            channel = conversation.Channel,
            direction = conversation.Direction,
            profileId = result?.ProfileId ?? conversation.Profile?.ItemId,
            sessionId = conversation.Session?.SessionId,
            activityId = conversation.ActivityId,
        });
    }
}
