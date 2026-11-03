namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Takes part in an AI conversation before its first message: it can set the variables the conversation starts
/// with, choose the profile, or decline the conversation. Workflows take part through the
/// <c>AI Conversation Starting</c> event.
/// </summary>
public interface IAIConversationStartingHandler
{
    /// <summary>
    /// Called before the conversation's first message.
    /// </summary>
    /// <param name="conversation">The conversation about to start.</param>
    /// <param name="result">The decisions so far, which the handler adds to.</param>
    /// <param name="cancellationToken">The token cancelled when the channel's time budget runs out.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task StartingAsync(AIConversationContext conversation, AIConversationStartingResult result, CancellationToken cancellationToken);
}
