using CrestApps.AI.Handlers;
using CrestApps.AI.Chat;
using CrestApps.AI.Chat.Handlers;
using CrestApps.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Triggers Orchard workflow events after the shared framework post-close
/// processor finishes the configured post-session tasks.
/// </summary>
public sealed class PostSessionProcessingChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly IWorkflowManager _workflowManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PostSessionProcessingChatSessionHandler> _logger;

    public PostSessionProcessingChatSessionHandler(
        IWorkflowManager workflowManager,
        TimeProvider timeProvider,
        ILogger<PostSessionProcessingChatSessionHandler> logger)
    {
        _workflowManager = workflowManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        if (!context.Items.TryGetValue(AIChatSessionHandlerContextKeys.PostCloseProcessingResult, out var value)
            || value is not AIChatSessionPostCloseProcessingResult result
            || !result.PostSessionTasksCompletedNow)
        {
            return;
        }

        try
        {
            await TriggerPostProcessedEventAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to trigger post-session workflow events for session '{SessionId}'.",
                context.ChatSession.SessionId);
        }
    }

    private async Task TriggerPostProcessedEventAsync(ChatMessageCompletedContext context)
    {
        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", context.ChatSession.SessionId },
                { "ProfileId", context.Profile.ItemId },
                { "Session", context.ChatSession },
                { "Profile", context.Profile },
                { "Results", context.ChatSession.PostSessionResults },
                { "Timestamp", _timeProvider.GetUtcNow().UtcDateTime },
            };

            await _workflowManager.TriggerEventAsync(
                nameof(AIChatSessionPostProcessedEvent),
                input,
                correlationId: context.ChatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionPostProcessedEvent for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
