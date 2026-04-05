using CrestApps.AI.Chat;
using CrestApps.AI.Handlers;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.Mvc.Web.Areas.AIChat.Handlers;

public sealed class AnalyticsChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly MvcAIChatSessionEventService _eventService;
    private readonly ILogger<AnalyticsChatSessionHandler> _logger;

    public AnalyticsChatSessionHandler(
        MvcAIChatSessionEventService eventService,
        ILogger<AnalyticsChatSessionHandler> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        var analyticsMetadata = context.Profile.As<AnalyticsMetadata>();

        if (!analyticsMetadata.EnableSessionMetrics)
        {
            return;
        }

        try
        {
            var userMessageCount = context.Prompts.Count(p => p.Role == ChatRole.User);

            if (userMessageCount == 1)
            {
                await _eventService.RecordSessionStartedAsync(context.ChatSession);
            }

            if (context.InputTokenCount > 0 || context.OutputTokenCount > 0 || context.ResponseLatencyMs > 0)
            {
                await _eventService.RecordCompletionMetricsAsync(
                    context.ChatSession.SessionId,
                    context.InputTokenCount,
                    context.OutputTokenCount,
                    context.ResponseLatencyMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record analytics event for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
