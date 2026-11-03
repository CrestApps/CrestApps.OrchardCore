namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Called for each turn of an AI conversation as it is saved, on every channel, while the conversation is going
/// on. Workflows take part through the <c>AI Conversation Turn</c> event.
/// </summary>
public interface IAIConversationTurnHandler
{
    /// <summary>
    /// Called after a turn is saved.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="turn">The turn.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task TurnAsync(AIConversationContext conversation, AIConversationTurn turn, CancellationToken cancellationToken);
}
