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

    public AIChatSessionEventPostCloseObserver(AIChatSessionEventService eventService)
    {
        _eventService = eventService;
    }

    public async Task RecordSessionEndedAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        bool isResolved,
        CancellationToken cancellationToken = default) =>
        await _eventService.RecordSessionEndedAsync(session, prompts.Count, isResolved);

    public async Task RecordConversionGoalsAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<ConversionGoalResult> goalResults,
        CancellationToken cancellationToken = default) =>
        await _eventService.RecordConversionMetricsAsync(session.SessionId, goalResults.ToList());
}
