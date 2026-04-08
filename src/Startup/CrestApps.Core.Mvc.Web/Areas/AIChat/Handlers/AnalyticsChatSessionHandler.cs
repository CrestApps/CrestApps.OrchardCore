using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Handlers;

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
