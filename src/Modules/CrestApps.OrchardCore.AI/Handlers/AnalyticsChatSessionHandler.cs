using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that records analytics events
/// when chat sessions are active and closed (for the AI Chat Session metrics feature).
/// Only records metrics when the profile has session metrics enabled.
/// </summary>
public sealed class AnalyticsChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly AIChatSessionEventService _eventService;
    private readonly ILogger<AnalyticsChatSessionHandler> _logger;

    public AnalyticsChatSessionHandler(
        AIChatSessionEventService eventService,
        ILogger<AnalyticsChatSessionHandler> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        var analyticsMetadata = context.Profile.GetOrCreate<AnalyticsMetadata>();

        if (!analyticsMetadata.EnableSessionMetrics)
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

            if (context.ResponseLatencyMs > 0)
            {
                await _eventService.RecordResponseLatencyAsync(context.ChatSession.SessionId, context.ResponseLatencyMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record analytics event for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
