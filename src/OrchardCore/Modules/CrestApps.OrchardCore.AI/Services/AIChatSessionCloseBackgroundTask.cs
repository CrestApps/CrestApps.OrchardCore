using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Services;

[BackgroundTask(
    Title = "AI Chat Session Close",
    Schedule = "*/10 * * * *",
    Description = "Periodically closes inactive AI chat sessions, retries pending post-close processing, and triggers workflow events.",
    LockTimeout = 5_000,
    LockExpiration = 300_00)]
public sealed class AIChatSessionCloseBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Maximum number of post-close processing attempts before marking as failed.
    /// </summary>

    private const int MaxPostCloseAttempts = 3;
    /// <summary>
    /// Minimum delay between post-close processing retry attempts.
    /// </summary>

    private static readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(5);

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var promptStore = serviceProvider.GetRequiredService<IAIChatSessionPromptStore>();

        var logger = serviceProvider.GetRequiredService<ILogger<AIChatSessionCloseBackgroundTask>>();

        var utcNow = clock.UtcNow;

        // Get all chat profiles that have data extraction enabled.

        var profiles = await profileManager.GetAsync(AIProfileType.Chat);

        foreach (var profile in profiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;

            }

            // Phase 1: Close inactive sessions.

            await CloseInactiveSessionsAsync(serviceProvider, session, profile, promptStore, utcNow, logger, cancellationToken);

            // Phase 2: Retry pending post-close processing for already-closed sessions.
            await RetryPendingProcessingAsync(serviceProvider, session, profile, promptStore, utcNow, logger, cancellationToken);
        }

    }

    private static async Task CloseInactiveSessionsAsync(
        IServiceProvider serviceProvider,
        ISession session,
        AIProfile profile,
        IAIChatSessionPromptStore promptStore,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {

        var settings = profile.GetSettings<AIProfileDataExtractionSettings>();

        if (!settings.EnableDataExtraction || settings.SessionInactivityTimeoutInMinutes <= 0)
        {
            return;

        }

        var timeout = TimeSpan.FromMinutes(settings.SessionInactivityTimeoutInMinutes);

        var cutoffUtc = utcNow - timeout;

        // Query active sessions that are past the inactivity timeout.
        var inactiveSessions = await session.Query<AIChatSession, AIChatSessionIndex>(
            i => i.ProfileId == profile.ItemId && i.Status == ChatSessionStatus.Active && i.LastActivityUtc < cutoffUtc,
            collection: AIConstants.AICollectionName)

                .ListAsync(cancellationToken);

        foreach (var chatSession in inactiveSessions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;

            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Closing inactive AI chat session '{SessionId}' for profile '{ProfileId}'. Last activity: {LastActivity}.",
                    chatSession.SessionId,
                    profile.ItemId,
                    chatSession.LastActivityUtc);

            }

            chatSession.Status = ChatSessionStatus.Closed;

            chatSession.ClosedAtUtc = utcNow;

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            // Run all post-close processing (tasks + analytics + conversion goals) as a unified resilient pipeline.

            await RunPostCloseProcessingAsync(serviceProvider, profile, chatSession, prompts, utcNow, logger, cancellationToken);

            await session.SaveAsync(chatSession, false, collection: AIConstants.AICollectionName, cancellationToken);

            await TriggerSessionClosedWorkflowAsync(serviceProvider, profile, chatSession, utcNow, logger);
        }

    }

    private static async Task RetryPendingProcessingAsync(
        IServiceProvider serviceProvider,
        ISession session,
        AIProfile profile,
        IAIChatSessionPromptStore promptStore,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Find closed sessions with pending post-close processing that are due for retry.
        var pendingSessions = await session.Query<AIChatSession, AIChatSessionIndex>(
            i => i.ProfileId == profile.ItemId
                && i.Status == ChatSessionStatus.Closed
                    && i.PostSessionProcessingStatus == PostSessionProcessingStatus.Pending,
            collection: AIConstants.AICollectionName)

                .ListAsync(cancellationToken);

        foreach (var chatSession in pendingSessions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;

            }

            // Skip if maximum attempts have been reached.

            if (chatSession.PostSessionProcessingAttempts >= MaxPostCloseAttempts)
            {
                chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Failed;

                await session.SaveAsync(chatSession, false, collection: AIConstants.AICollectionName, cancellationToken);

                logger.LogWarning(
                    "Post-close processing for session '{SessionId}' has exceeded maximum attempts ({MaxAttempts}). Marking as failed. "
                    + "Tasks processed: {TasksProcessed}, Analytics recorded: {AnalyticsRecorded}, Conversion goals evaluated: {ConversionGoalsEvaluated}.",
                    chatSession.SessionId,
                    MaxPostCloseAttempts,
                    chatSession.IsPostSessionTasksProcessed,
                    chatSession.IsAnalyticsRecorded,

                    chatSession.IsConversionGoalsEvaluated);

                continue;

            }

            // Apply retry delay: skip if last attempt was too recent.

            if (chatSession.PostSessionProcessingLastAttemptUtc.HasValue
                && utcNow - chatSession.PostSessionProcessingLastAttemptUtc.Value < _retryDelay)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Skipping retry for session '{SessionId}': last attempt was at {LastAttemptUtc}, retry delay not elapsed.",
                        chatSession.SessionId,
                        chatSession.PostSessionProcessingLastAttemptUtc.Value);

                }

                continue;

            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Retrying post-close processing for session '{SessionId}' (attempt {Attempt}/{MaxAttempts}). "
                    + "Tasks processed: {TasksProcessed}, Analytics recorded: {AnalyticsRecorded}, Conversion goals evaluated: {ConversionGoalsEvaluated}.",
                    chatSession.SessionId,
                    chatSession.PostSessionProcessingAttempts + 1,
                    MaxPostCloseAttempts,
                    chatSession.IsPostSessionTasksProcessed,
                    chatSession.IsAnalyticsRecorded,
                    chatSession.IsConversionGoalsEvaluated);

            }

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

            await RunPostCloseProcessingAsync(serviceProvider, profile, chatSession, prompts, utcNow, logger, cancellationToken);

            await session.SaveAsync(chatSession, false, collection: AIConstants.AICollectionName, cancellationToken);
        }

    }
    /// <summary>
    /// Runs all post-close processing steps as a unified pipeline.
    /// Each step (post-session tasks, analytics, conversion goals) is tracked independently
    /// so successful steps are not re-run on retry.
    /// </summary>
    private static async Task RunPostCloseProcessingAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();

        var analyticsMetadata = profile.As<AnalyticsMetadata>();

        var needsPostSessionTasks = !chatSession.IsPostSessionTasksProcessed
            && postSessionSettings.EnablePostSessionProcessing

                && postSessionSettings.PostSessionTasks.Count > 0;

        var needsAnalytics = !chatSession.IsAnalyticsRecorded

            && (analyticsMetadata.EnableSessionMetrics || analyticsMetadata.EnableAIResolutionDetection);

        var needsConversionGoals = !chatSession.IsConversionGoalsEvaluated
            && analyticsMetadata.EnableConversionMetrics

                && analyticsMetadata.ConversionGoals.Count > 0;

        if (!needsPostSessionTasks && !needsAnalytics && !needsConversionGoals)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "No post-close processing needed for session '{SessionId}'. All steps are either completed or not configured.",
                    chatSession.SessionId);

            }

            return;

        }

        // Mark as pending and track the attempt.
        chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Pending;
        chatSession.PostSessionProcessingAttempts++;

        chatSession.PostSessionProcessingLastAttemptUtc = utcNow;

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Starting post-close processing for session '{SessionId}' (attempt {Attempt}). "
                + "Steps to run: PostSessionTasks={NeedsTasks}, Analytics={NeedsAnalytics}, ConversionGoals={NeedsConversion}.",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts,
                needsPostSessionTasks,
                needsAnalytics,
                needsConversionGoals);

        }

        // Step 1: Post-session tasks (custom AI analysis, emails, etc.)

        if (needsPostSessionTasks)
        {
            await RunPostSessionTasksAsync(serviceProvider, profile, chatSession, prompts, logger, cancellationToken);

        }

        // Step 2: Analytics recording (session-end metrics + resolution detection).

        if (needsAnalytics)
        {
            await RecordSessionAnalyticsAsync(serviceProvider, profile, chatSession, prompts, analyticsMetadata, logger);

        }

        // Step 3: Conversion goals evaluation.

        if (needsConversionGoals)
        {
            await EvaluateConversionGoalsAsync(serviceProvider, profile, chatSession, prompts, analyticsMetadata, logger);

        }

        // Determine overall status: Completed only when all applicable steps are done.
        var tasksComplete = chatSession.IsPostSessionTasksProcessed
            || !postSessionSettings.EnablePostSessionProcessing

                || postSessionSettings.PostSessionTasks.Count == 0;

        var analyticsComplete = chatSession.IsAnalyticsRecorded

            || (!analyticsMetadata.EnableSessionMetrics && !analyticsMetadata.EnableAIResolutionDetection);

        var conversionComplete = chatSession.IsConversionGoalsEvaluated
            || !analyticsMetadata.EnableConversionMetrics

                || analyticsMetadata.ConversionGoals.Count == 0;

        if (tasksComplete && analyticsComplete && conversionComplete)
        {

            chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Completed;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Post-close processing completed for session '{SessionId}' after {Attempts} attempt(s).",
                    chatSession.SessionId,
                    chatSession.PostSessionProcessingAttempts);
            }
        }
        else
        {
            logger.LogWarning(
                "Post-close processing incomplete for session '{SessionId}' after attempt {Attempt}. "
                + "Tasks: {TasksProcessed}, Analytics: {AnalyticsRecorded}, Conversion goals: {ConversionGoalsEvaluated}.",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts,
                chatSession.IsPostSessionTasksProcessed,
                chatSession.IsAnalyticsRecorded,
                chatSession.IsConversionGoalsEvaluated);
        }

    }

    private static async Task RunPostSessionTasksAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();
        var taskNames = postSessionSettings.PostSessionTasks.Select(t => t.Name).ToList();

        var clock = serviceProvider.GetRequiredService<IClock>();

        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Running post-session tasks for session '{SessionId}'. Configured tasks: [{TaskNames}].",
                    chatSession.SessionId,
                    string.Join(',', taskNames));

            }

            // Initialize any tasks not yet tracked as Pending.

            foreach (var taskName in taskNames)
            {
                if (!chatSession.PostSessionResults.ContainsKey(taskName))
                {
                    chatSession.PostSessionResults[taskName] = new PostSessionResult
                    {
                        Name = taskName,
                        Status = PostSessionTaskResultStatus.Pending,
                    };
                }

            }

            // Increment attempts for non-succeeded tasks before processing.

            foreach (var taskName in taskNames)
            {
                if (chatSession.PostSessionResults.TryGetValue(taskName, out var existing)
                    && existing.Status != PostSessionTaskResultStatus.Succeeded)
                {
                    existing.Attempts++;
                }

            }

            var postSessionService = serviceProvider.GetService<PostSessionProcessingService>();

            if (postSessionService is null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("PostSessionProcessingService is not available. Skipping post-session tasks for session '{SessionId}'.", chatSession.SessionId);
                }

                return;

            }

            var results = await postSessionService.ProcessAsync(profile, chatSession, prompts, cancellationToken);

            // Merge new results into the session's PostSessionResults.

            if (results is not null && results.Count > 0)
            {
                foreach (var (taskName, result) in results)
                {
                    // Preserve the attempt count from existing tracking.

                    if (chatSession.PostSessionResults.TryGetValue(taskName, out var existing))
                    {
                        result.Attempts = existing.Attempts;

                    }

                    chatSession.PostSessionResults[taskName] = result;

                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Post-session task '{TaskName}' for session '{SessionId}': Status={Status}, Value='{Value}'.",
                            taskName,
                            chatSession.SessionId,
                            result.Status,
                            result.Value?.Length > 100 ? result.Value[..100] + "..." : result.Value);
                    }
                }

            }

            // Permanently fail tasks that produced no result and have exhausted retry attempts.

            var utcNow = clock.UtcNow;

            foreach (var taskName in taskNames)
            {
                if (chatSession.PostSessionResults.TryGetValue(taskName, out var result)
                    && result.Status != PostSessionTaskResultStatus.Succeeded
                        && result.Attempts >= MaxPostCloseAttempts)
                {
                    result.Status = PostSessionTaskResultStatus.Failed;
                    result.ProcessedAtUtc = utcNow;

                    result.ErrorMessage ??= $"Task produced no result after {result.Attempts} attempt(s).";

                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Post-session task '{TaskName}' for session '{SessionId}' permanently failed after {Attempts} attempt(s).",
                            taskName,
                            chatSession.SessionId,
                            result.Attempts);
                    }
                }

            }

            // Tasks are fully processed when all are either Succeeded or permanently Failed.
            var allProcessed = taskNames.All(name =>
            chatSession.PostSessionResults.TryGetValue(name, out var r)
                && (r.Status == PostSessionTaskResultStatus.Succeeded

                    || (r.Status == PostSessionTaskResultStatus.Failed && r.Attempts >= MaxPostCloseAttempts)));

            chatSession.IsPostSessionTasksProcessed = allProcessed;

            if (logger.IsEnabled(LogLevel.Information))
            {
                var succeededCount = chatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Succeeded);
                var failedCount = chatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Failed);

                var pendingCount = chatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Pending);

                logger.LogInformation(
                    "Post-session tasks for session '{SessionId}': {Succeeded} succeeded, {Failed} failed, {Pending} pending out of {Total} total.",
                    chatSession.SessionId,
                    succeededCount,
                    failedCount,
                    pendingCount,
                    taskNames.Count);

            }

            var workflowManager = serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager is not null && allProcessed)
            {
                var input = new Dictionary<string, object>
                {
                    { "SessionId", chatSession.SessionId },
                    { "ProfileId", profile.ItemId },
                    { "Session", chatSession },
                    { "Profile", profile },
                    { "Results", chatSession.PostSessionResults },

                };

                await workflowManager.TriggerEventAsync(
                    nameof(AIChatSessionPostProcessedEvent),
                input,
                correlationId: chatSession.SessionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Post-session tasks failed for session '{SessionId}' (attempt {Attempt}). Tasks: [{TaskNames}].",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts,

                string.Join(", ", taskNames));

            // Record error on all non-succeeded tasks; permanently fail those that have exhausted attempts.

            var utcNow = clock.UtcNow;

            foreach (var taskName in taskNames)
            {
                if (chatSession.PostSessionResults.TryGetValue(taskName, out var result)
                    && result.Status != PostSessionTaskResultStatus.Succeeded)
                {

                    result.ErrorMessage = ex.Message;

                    if (result.Attempts >= MaxPostCloseAttempts)
                    {
                        result.Status = PostSessionTaskResultStatus.Failed;
                        result.ProcessedAtUtc = utcNow;
                    }
                }
            }
        }

    }

    private static async Task RecordSessionAnalyticsAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        AnalyticsMetadata analyticsMetadata,
        ILogger logger)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Recording session analytics for session '{SessionId}'.", chatSession.SessionId);

            }

            var eventService = serviceProvider.GetService<AIChatSessionEventService>();

            if (eventService is null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("AIChatSessionEventService is not available. Skipping analytics for session '{SessionId}'.", chatSession.SessionId);

                }

                return;

            }

            var isResolved = false;

            // Use AI resolution detection when enabled instead of assuming abandoned.

            if (analyticsMetadata.EnableAIResolutionDetection)
            {

                var postSessionService = serviceProvider.GetService<PostSessionProcessingService>();

                if (postSessionService is not null)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Evaluating AI resolution detection for session '{SessionId}'.", chatSession.SessionId);

                    }

                    isResolved = await postSessionService.EvaluateResolutionAsync(profile, prompts);

                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Resolution detection for session '{SessionId}': IsResolved={IsResolved}.",
                            chatSession.SessionId,
                            isResolved);
                    }
                }

            }

            await eventService.RecordSessionEndedAsync(chatSession, prompts.Count, isResolved);

            chatSession.IsAnalyticsRecorded = true;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Session analytics recorded for session '{SessionId}'. IsResolved={IsResolved}, MessageCount={MessageCount}.",
                    chatSession.SessionId,
                    isResolved,
                    prompts.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to record session analytics for session '{SessionId}' (attempt {Attempt}).",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts);
        }

    }

    private static async Task EvaluateConversionGoalsAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        AnalyticsMetadata analyticsMetadata,
        ILogger logger)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Evaluating conversion goals for session '{SessionId}' ({GoalCount} goal(s) configured).",
                    chatSession.SessionId,
                    analyticsMetadata.ConversionGoals.Count);

            }

            var postSessionService = serviceProvider.GetService<PostSessionProcessingService>();

            if (postSessionService is null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("PostSessionProcessingService is not available. Skipping conversion goals for session '{SessionId}'.", chatSession.SessionId);

                }

                return;

            }

            var goalResults = await postSessionService.EvaluateConversionGoalsAsync(

                profile, prompts, analyticsMetadata.ConversionGoals);

            if (goalResults is not null && goalResults.Count > 0)
            {

                var eventService = serviceProvider.GetService<AIChatSessionEventService>();

                if (eventService is not null)
                {
                    await eventService.RecordConversionMetricsAsync(chatSession.SessionId, goalResults);

                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Conversion goals evaluated for session '{SessionId}': {GoalCount} goal(s), total score {Score}/{MaxScore}.",
                        chatSession.SessionId,
                        goalResults.Count,
                        goalResults.Sum(r => r.Score),
                    goalResults.Sum(r => r.MaxScore));
                }
            }
            else
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Conversion goals evaluation returned no results for session '{SessionId}'.",
                        chatSession.SessionId);
                }

            }

            chatSession.IsConversionGoalsEvaluated = true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to evaluate conversion goals for session '{SessionId}' (attempt {Attempt}).",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts);
        }

    }

    private static async Task TriggerSessionClosedWorkflowAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        DateTime utcNow,
        ILogger logger)
    {

        var workflowManager = serviceProvider.GetService<IWorkflowManager>();

        if (workflowManager is null)
        {
            return;

        }

        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", chatSession.SessionId },
                { "ProfileId", profile.ItemId },
                { "Session", chatSession },
                { "Profile", profile },
                { "ClosedAtUtc", utcNow },

            };

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionClosedEvent),
            input,
            correlationId: chatSession.SessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger AIChatSessionClosedEvent for session '{SessionId}'.", chatSession.SessionId);
        }
    }
}
