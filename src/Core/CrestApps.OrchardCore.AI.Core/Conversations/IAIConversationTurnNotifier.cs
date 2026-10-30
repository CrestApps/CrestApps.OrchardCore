using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Tells the <see cref="IAIConversationTurnHandler"/>s about a turn a channel just saved.
/// </summary>
public interface IAIConversationTurnNotifier
{
    /// <summary>
    /// Tells the handlers about a turn. Failures are logged and never reach the conversation.
    /// </summary>
    /// <param name="session">The AI session the turn was saved in.</param>
    /// <param name="role">Who spoke: <see cref="AIConversationTurn.CustomerRole"/> or <see cref="AIConversationTurn.AssistantRole"/>.</param>
    /// <param name="text">What was said.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the handlers are done.</returns>
    Task NotifyAsync(AIChatSession session, string role, string text, CancellationToken cancellationToken = default);
}
