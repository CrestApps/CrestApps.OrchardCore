using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatSessionEventPostCloseObserver :
    IAIChatSessionAnalyticsRecorder,
    IAIChatSessionConversionGoalRecorder
{
    private readonly MvcAIChatSessionEventService _eventService;

    public MvcAIChatSessionEventPostCloseObserver(MvcAIChatSessionEventService eventService)
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
