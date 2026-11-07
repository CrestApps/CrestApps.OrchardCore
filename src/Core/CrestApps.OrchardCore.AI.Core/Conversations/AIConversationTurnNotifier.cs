using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Runs the <see cref="IAIConversationTurnHandler"/>s for a saved turn, describing the conversation from the scope
/// the channel made current, or from the session alone when the channel made none.
/// </summary>
public sealed class AIConversationTurnNotifier : IAIConversationTurnNotifier
{
    private readonly IEnumerable<IAIConversationTurnHandler> _handlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationTurnNotifier"/> class.
    /// </summary>
    /// <param name="handlers">The turn handlers.</param>
    /// <param name="clock">The clock a turn is stamped with.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationTurnNotifier(IEnumerable<IAIConversationTurnHandler> handlers, IClock clock, ILogger<AIConversationTurnNotifier> logger)
    {
        _handlers = handlers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotifyAsync(AIChatSession session, string role, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || !_handlers.Any())
        {
            return;
        }

        var conversation = AIConversationScope.Resolve();

        if (conversation is null || (session is not null && conversation.Session?.SessionId != session.SessionId))
        {
            conversation = new AIConversationContext { Session = session };
        }

        var turn = new AIConversationTurn(role, text.Trim(), _clock.UtcNow);

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.TurnAsync(conversation, turn, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "A conversation turn handler '{Handler}' failed; the conversation goes on.", handler.GetType().Name);
            }
        }
    }
}
