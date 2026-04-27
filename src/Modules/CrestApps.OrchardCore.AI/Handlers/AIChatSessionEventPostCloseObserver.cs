using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Persists Orchard chat-session analytics emitted by the shared post-close processor.
/// </summary>
public sealed class AIChatSessionEventPostCloseObserver :
    IAIChatSessionAnalyticsRecorder,
    IAIChatSessionConversionGoalRecorder
{
    private readonly AIChatSessionEventService _eventService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionEventPostCloseObserver"/> class.
    /// </summary>
    /// <param name="eventService">The service used to persist chat session analytics events.</param>
    public AIChatSessionEventPostCloseObserver(AIChatSessionEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>
    /// Records that a chat session has ended, including the prompt count and resolution status.
    /// </summary>
    /// <param name="profile">The AI profile associated with the session.</param>
    /// <param name="session">The chat session that ended.</param>
    /// <param name="prompts">The prompts exchanged during the session.</param>
    /// <param name="isResolved">Whether the session was resolved.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task RecordSessionEndedAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        bool isResolved,
        CancellationToken cancellationToken = default) =>
        await _eventService.RecordSessionEndedAsync(session, prompts.Count, isResolved);

    /// <summary>
    /// Records conversion goal results for a chat session.
    /// </summary>
    /// <param name="profile">The AI profile associated with the session.</param>
    /// <param name="session">The chat session for which goals were evaluated.</param>
    /// <param name="goalResults">The conversion goal results to record.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task RecordConversionGoalsAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<ConversionGoalResult> goalResults,
        CancellationToken cancellationToken = default) =>
        await _eventService.RecordConversionMetricsAsync(session.SessionId, goalResults.ToList());
}
