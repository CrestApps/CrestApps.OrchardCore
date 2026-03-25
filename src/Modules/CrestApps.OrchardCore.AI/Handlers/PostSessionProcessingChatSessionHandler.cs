using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var taskNames = settings.PostSessionTasks.Select(t => t.Name).ToList();

        // Mark as pending so the background task can retry if this attempt fails.
        context.ChatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Pending;

        try
        {
            context.ChatSession.PostSessionProcessingAttempts++;
            context.ChatSession.PostSessionProcessingLastAttemptUtc = _clock.UtcNow;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Running inline post-session tasks for session '{SessionId}' (attempt {Attempt}). Tasks: [{TaskNames}].",
                    context.ChatSession.SessionId,
                    context.ChatSession.PostSessionProcessingAttempts,
                    string.Join(", ", taskNames));
            }

            // Initialize any tasks not yet tracked as Pending.
            foreach (var taskName in taskNames)
            {
                if (!context.ChatSession.PostSessionResults.ContainsKey(taskName))
                {
                    context.ChatSession.PostSessionResults[taskName] = new PostSessionResult
                    {
                        Name = taskName,
                        Status = PostSessionTaskResultStatus.Pending,
                    };
                }
            }

            var results = await _postSessionProcessingService.ProcessAsync(
                context.Profile,
                context.ChatSession,
                context.Prompts);

            // Merge new results into the session's PostSessionResults.
            if (results is not null && results.Count > 0)
            {
                foreach (var (taskName, result) in results)
                {
                    context.ChatSession.PostSessionResults[taskName] = result;

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Post-session task '{TaskName}' for session '{SessionId}': Status={Status}, Value='{Value}'.",
                            taskName,
                            context.ChatSession.SessionId,
                            result.Status,
                            result.Value?.Length > 100 ? result.Value[..100] + "..." : result.Value);
                    }
                }
            }

            // Determine if all tasks are now Succeeded.
            var allSucceeded = taskNames.All(name =>
                context.ChatSession.PostSessionResults.TryGetValue(name, out var r)
                && r.Status == PostSessionTaskResultStatus.Succeeded);

            context.ChatSession.IsPostSessionTasksProcessed = allSucceeded;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                var succeededCount = context.ChatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Succeeded);
                var failedCount = context.ChatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Failed);
                var pendingCount = context.ChatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Pending);

                _logger.LogInformation(
                    "Inline post-session tasks for session '{SessionId}': {Succeeded} succeeded, {Failed} failed, {Pending} pending out of {Total} total.",
                    context.ChatSession.SessionId,
                    succeededCount,
                    failedCount,
                    pendingCount,
                    taskNames.Count);
            }

            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager is not null && allSucceeded)
            {
                await TriggerPostProcessedEventAsync(workflowManager, context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Inline post-session tasks failed for session '{SessionId}' (attempt {Attempt}). Tasks: [{TaskNames}]. Will be retried by background task.",
                context.ChatSession.SessionId,
                context.ChatSession.PostSessionProcessingAttempts,
                string.Join(", ", taskNames));

            // Mark all non-succeeded tasks as Failed.
            foreach (var taskName in taskNames)
            {
                if (context.ChatSession.PostSessionResults.TryGetValue(taskName, out var result)
                    && result.Status != PostSessionTaskResultStatus.Succeeded)
                {
                    result.Status = PostSessionTaskResultStatus.Failed;
                    result.ErrorMessage = ex.Message;
                    result.ProcessedAtUtc = _clock.UtcNow;
                }
            }
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
