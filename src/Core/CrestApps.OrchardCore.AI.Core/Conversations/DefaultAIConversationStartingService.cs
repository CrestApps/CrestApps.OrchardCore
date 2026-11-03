using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Runs the starting handlers in turn, each within what is left of the channel's time budget.
/// </summary>
/// <remarks>
/// A caller on the line is waiting to be answered, so an inbound call gets a short budget; an outbound call or text
/// has not been placed yet and can wait longer. The conversation fails open: a handler that throws or runs out of
/// time is logged, and the conversation starts with whatever was decided before it.
/// </remarks>
public sealed class DefaultAIConversationStartingService : IAIConversationStartingService
{
    /// <summary>
    /// The time an inbound conversation waits for its starting handlers.
    /// </summary>
    public static readonly TimeSpan InboundBudget = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The time an outbound conversation waits for its starting handlers.
    /// </summary>
    public static readonly TimeSpan OutboundBudget = TimeSpan.FromSeconds(30);

    private readonly IEnumerable<IAIConversationStartingHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIConversationStartingService"/> class.
    /// </summary>
    /// <param name="handlers">The starting handlers.</param>
    /// <param name="logger">The logger.</param>
    public DefaultAIConversationStartingService(
        IEnumerable<IAIConversationStartingHandler> handlers,
        ILogger<DefaultAIConversationStartingService> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AIConversationStartingResult> StartingAsync(AIConversationContext conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var result = new AIConversationStartingResult();
        var budget = conversation.Direction == AIConversationDirections.Inbound ? InboundBudget : OutboundBudget;

        using var budgetSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetSource.CancelAfter(budget);

        foreach (var handler in _handlers)
        {
            if (result.Declined)
            {
                break;
            }

            try
            {
                await handler.StartingAsync(conversation, result, budgetSource.Token).WaitAsync(budgetSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result.TimedOut = true;

                _logger.LogWarning(
                    "A conversation starting handler '{Handler}' ran past the {Budget} budget; the conversation starts with what was decided by then.",
                    handler.GetType().Name,
                    budget);

                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "A conversation starting handler '{Handler}' failed; the conversation starts without it.", handler.GetType().Name);
            }
        }

        if (result.Variables.Count > 0)
        {
            conversation.SetVariables(result.Variables);
        }

        return result;
    }
}
