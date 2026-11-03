namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Runs the <see cref="IAIConversationStartingHandler"/>s of a conversation that is about to start, within the
/// channel's time budget.
/// </summary>
public interface IAIConversationStartingService
{
    /// <summary>
    /// Runs the starting handlers for a conversation.
    /// </summary>
    /// <param name="conversation">The conversation about to start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What the handlers decided. A handler that fails or runs out of time is logged and skipped, so the
    /// conversation always starts with what was decided by then.</returns>
    Task<AIConversationStartingResult> StartingAsync(AIConversationContext conversation, CancellationToken cancellationToken = default);
}
