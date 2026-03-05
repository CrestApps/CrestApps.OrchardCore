using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that runs post-session processing tasks
/// after a chat session is closed. Triggers workflow events when processing completes.
/// </summary>
public sealed class PostSessionProcessingChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly PostSessionProcessingService _postSessionProcessingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public PostSessionProcessingChatSessionHandler(
        PostSessionProcessingService postSessionProcessingService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<PostSessionProcessingChatSessionHandler> logger)
    {
        _postSessionProcessingService = postSessionProcessingService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        // Only process when the session has just been closed.
        if (context.ChatSession.Status != ChatSessionStatus.Closed)
        {
            return;
        }

        // Skip if post-session tasks have already been processed.
        if (context.ChatSession.IsPostSessionTasksProcessed)
        {
            return;
        }

        // Skip if post-session processing is not enabled for this profile.
        var settings = context.Profile.GetSettings<AIProfilePostSessionSettings>();

        if (!settings.EnablePostSessionProcessing || settings.PostSessionTasks.Count == 0)
        {
            return;
        }

        // Mark as pending so the background task can retry if this attempt fails.
        context.ChatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Pending;

        try
        {
            context.ChatSession.PostSessionProcessingAttempts++;
            context.ChatSession.PostSessionProcessingLastAttemptUtc = _clock.UtcNow;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Running inline post-session tasks for session '{SessionId}' (attempt {Attempt}).",
                    context.ChatSession.SessionId,
                    context.ChatSession.PostSessionProcessingAttempts);
            }

            var results = await _postSessionProcessingService.ProcessAsync(
                context.Profile,
                context.ChatSession,
                context.Prompts);

            if (results is not null && results.Count > 0)
            {
                context.ChatSession.PostSessionResults = results;
            }

            context.ChatSession.IsPostSessionTasksProcessed = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Inline post-session tasks completed for session '{SessionId}' with {TaskCount} result(s).",
                    context.ChatSession.SessionId,
                    results?.Count ?? 0);
            }

            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager is not null)
            {
                await TriggerPostProcessedEventAsync(workflowManager, context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Inline post-session tasks failed for session '{SessionId}' (attempt {Attempt}). Will be retried by background task.",
                context.ChatSession.SessionId,
                context.ChatSession.PostSessionProcessingAttempts);
        }
    }

    private async Task TriggerPostProcessedEventAsync(
        IWorkflowManager workflowManager,
        ChatMessageCompletedContext context)
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
                { "Timestamp", _clock.UtcNow },
            };

            await workflowManager.TriggerEventAsync(
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
