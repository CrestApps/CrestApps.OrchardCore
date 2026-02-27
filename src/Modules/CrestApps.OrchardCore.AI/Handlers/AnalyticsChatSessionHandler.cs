using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that records analytics events
/// when chat sessions are active and closed (for the AI Chat Session metrics feature).
/// Only records metrics when the profile has session metrics enabled.
/// </summary>
public sealed class AnalyticsChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly AIChatSessionEventService _eventService;
    private readonly ILogger _logger;

    public AnalyticsChatSessionHandler(
        AIChatSessionEventService eventService,
        ILogger<AnalyticsChatSessionHandler> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        if (!context.Profile.As<AIProfileAnalyticsMetadata>().EnableSessionMetrics)
        {
            return;
        }

        try
        {
            // Record session start on first user message (message count = 1 user message).
            var userMessageCount = context.Prompts.Count(p => p.Role == ChatRole.User);

            if (userMessageCount == 1)
            {
                await _eventService.RecordSessionStartedAsync(context.ChatSession);
            }

            // Accumulate token usage and latency metrics for this completion.
            if (context.InputTokenCount > 0 || context.OutputTokenCount > 0 || context.ResponseLatencyMs > 0)
            {
                await _eventService.RecordCompletionMetricsAsync(
                    context.ChatSession.SessionId,
                    context.InputTokenCount,
                    context.OutputTokenCount,
                    context.ResponseLatencyMs);
            }

            // Record session end when session transitions to Closed.
            if (context.ChatSession.Status == ChatSessionStatus.Closed)
            {
                // Natural farewell = resolved.
                await _eventService.RecordSessionEndedAsync(context.ChatSession, context.Prompts.Count, isResolved: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record analytics event for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
