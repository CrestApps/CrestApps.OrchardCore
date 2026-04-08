using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Runs shared host-agnostic post-close chat session processing such as
/// post-session tasks, AI-based resolution analysis, and conversion-goal evaluation.
/// </summary>
public sealed class AIChatSessionPostCloseProcessor
{
    public const int MaxPostCloseAttempts = 3;

    private readonly PostSessionProcessingService _postSessionProcessingService;
    private readonly IEnumerable<IAIChatSessionAnalyticsRecorder> _analyticsRecorders;
    private readonly IEnumerable<IAIChatSessionConversionGoalRecorder> _conversionGoalRecorders;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AIChatSessionPostCloseProcessor> _logger;

    public AIChatSessionPostCloseProcessor(
        PostSessionProcessingService postSessionProcessingService,
        IEnumerable<IAIChatSessionAnalyticsRecorder> analyticsRecorders,
        IEnumerable<IAIChatSessionConversionGoalRecorder> conversionGoalRecorders,
        TimeProvider timeProvider,
        ILogger<AIChatSessionPostCloseProcessor> logger)
    {
        _postSessionProcessingService = postSessionProcessingService;
        _analyticsRecorders = analyticsRecorders;
        _conversionGoalRecorders = conversionGoalRecorders;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public static bool NeedsProcessing(AIProfile profile, AIChatSession chatSession)
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

        return needsPostSessionTasks || needsAnalytics || needsConversionGoals;
    }

    public async Task<AIChatSessionPostCloseProcessingResult> ProcessAsync(
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        CancellationToken cancellationToken = default)
    {
        var result = new AIChatSessionPostCloseProcessingResult();

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
            result.IsCompleted = true;
            return result;
        }

        result.HadWork = true;
        var startedWithCompletedTasks = chatSession.IsPostSessionTasksProcessed;
        var startedWithAnalytics = chatSession.IsAnalyticsRecorded;
        var startedWithGoals = chatSession.IsConversionGoalsEvaluated;

        chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Pending;
        chatSession.PostSessionProcessingAttempts++;
        chatSession.PostSessionProcessingLastAttemptUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Starting shared post-close processing for session '{SessionId}' (attempt {Attempt}). PostSessionTasks={NeedsTasks}, Analytics={NeedsAnalytics}, ConversionGoals={NeedsConversion}.",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts,
                needsPostSessionTasks,
                needsAnalytics,
                needsConversionGoals);
        }

        if (needsPostSessionTasks)
        {
            await RunPostSessionTasksAsync(profile, chatSession, prompts, cancellationToken);
        }

        if (needsAnalytics)
        {
            await RecordSessionAnalyticsAsync(profile, chatSession, prompts, cancellationToken);
        }

        if (needsConversionGoals)
        {
            await EvaluateConversionGoalsAsync(profile, chatSession, prompts, analyticsMetadata, cancellationToken);
        }

        var tasksComplete = chatSession.IsPostSessionTasksProcessed
            || !postSessionSettings.EnablePostSessionProcessing
            || postSessionSettings.PostSessionTasks.Count == 0;

        var analyticsComplete = chatSession.IsAnalyticsRecorded
            || (!analyticsMetadata.EnableSessionMetrics && !analyticsMetadata.EnableAIResolutionDetection);

        var conversionComplete = chatSession.IsConversionGoalsEvaluated
            || !analyticsMetadata.EnableConversionMetrics
            || analyticsMetadata.ConversionGoals.Count == 0;

        result.PostSessionTasksCompletedNow = !startedWithCompletedTasks && chatSession.IsPostSessionTasksProcessed;
        result.AnalyticsRecordedNow = !startedWithAnalytics && chatSession.IsAnalyticsRecorded;
        result.ConversionGoalsEvaluatedNow = !startedWithGoals && chatSession.IsConversionGoalsEvaluated;
        result.IsCompleted = tasksComplete && analyticsComplete && conversionComplete;

        if (result.IsCompleted)
        {
            chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Completed;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Shared post-close processing completed for session '{SessionId}' after {Attempts} attempt(s).",
                    chatSession.SessionId,
                    chatSession.PostSessionProcessingAttempts);
            }
        }
        else
        {
            _logger.LogWarning(
                "Shared post-close processing incomplete for session '{SessionId}' after attempt {Attempt}. Tasks={TasksProcessed}, Analytics={AnalyticsRecorded}, ConversionGoals={ConversionGoalsEvaluated}.",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts,
                chatSession.IsPostSessionTasksProcessed,
                chatSession.IsAnalyticsRecorded,
                chatSession.IsConversionGoalsEvaluated);
        }

        return result;
    }

    private async Task RunPostSessionTasksAsync(
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        CancellationToken cancellationToken)
    {
        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();
        var taskNames = postSessionSettings.PostSessionTasks.Select(t => t.Name).ToList();

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Running shared post-session tasks for session '{SessionId}'. Configured tasks: [{TaskNames}].",
                    chatSession.SessionId,
                    string.Join(',', taskNames));
            }

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

            foreach (var taskName in taskNames)
            {
                if (chatSession.PostSessionResults.TryGetValue(taskName, out var existing)
                    && existing.Status != PostSessionTaskResultStatus.Succeeded)
                {
                    existing.Attempts++;
                }
            }

            var results = await _postSessionProcessingService.ProcessAsync(profile, chatSession, prompts, cancellationToken);

            if (results is not null && results.Count > 0)
            {
                foreach (var (taskName, taskResult) in results)
                {
                    if (chatSession.PostSessionResults.TryGetValue(taskName, out var existing))
                    {
                        taskResult.Attempts = existing.Attempts;
                    }

                    chatSession.PostSessionResults[taskName] = taskResult;

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Shared post-session task '{TaskName}' for session '{SessionId}': Status={Status}, Value='{Value}'.",
                            taskName,
                            chatSession.SessionId,
                            taskResult.Status,
                            taskResult.Value?.Length > 100 ? taskResult.Value[..100] + "..." : taskResult.Value);
                    }
                }
            }

            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

            foreach (var taskName in taskNames)
            {
                if (chatSession.PostSessionResults.TryGetValue(taskName, out var taskResult)
                    && taskResult.Status != PostSessionTaskResultStatus.Succeeded
                    && taskResult.Attempts >= MaxPostCloseAttempts)
                {
                    taskResult.Status = PostSessionTaskResultStatus.Failed;
                    taskResult.ProcessedAtUtc = utcNow;
                    taskResult.ErrorMessage ??= $"Task produced no result after {taskResult.Attempts} attempt(s).";
                }
            }

            chatSession.IsPostSessionTasksProcessed = taskNames.All(name =>
                chatSession.PostSessionResults.TryGetValue(name, out var taskResult)
                && (taskResult.Status == PostSessionTaskResultStatus.Succeeded
                    || (taskResult.Status == PostSessionTaskResultStatus.Failed && taskResult.Attempts >= MaxPostCloseAttempts)));

            if (_logger.IsEnabled(LogLevel.Information))
            {
                var succeededCount = chatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Succeeded);
                var failedCount = chatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Failed);
                var pendingCount = chatSession.PostSessionResults.Values.Count(r => r.Status == PostSessionTaskResultStatus.Pending);

                _logger.LogInformation(
                    "Shared post-session tasks for session '{SessionId}': {Succeeded} succeeded, {Failed} failed, {Pending} pending out of {Total} total.",
                    chatSession.SessionId,
                    succeededCount,
                    failedCount,
                    pendingCount,
                    taskNames.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Shared post-session tasks failed for session '{SessionId}' (attempt {Attempt}). Tasks: [{TaskNames}].",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts,
                string.Join(", ", taskNames));

            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

            foreach (var taskName in taskNames)
            {
                if (chatSession.PostSessionResults.TryGetValue(taskName, out var taskResult)
                    && taskResult.Status != PostSessionTaskResultStatus.Succeeded)
                {
                    taskResult.ErrorMessage = ex.Message;

                    if (taskResult.Attempts >= MaxPostCloseAttempts)
                    {
                        taskResult.Status = PostSessionTaskResultStatus.Failed;
                        taskResult.ProcessedAtUtc = utcNow;
                    }
                }
            }
        }
    }

    private async Task RecordSessionAnalyticsAsync(
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        CancellationToken cancellationToken)
    {
        try
        {
            var isResolved = false;

            if (profile.As<AnalyticsMetadata>().EnableAIResolutionDetection)
            {
                isResolved = await _postSessionProcessingService.EvaluateResolutionAsync(profile, prompts, cancellationToken);
            }

            foreach (var recorder in _analyticsRecorders)
            {
                await recorder.RecordSessionEndedAsync(profile, chatSession, prompts, isResolved, cancellationToken);
            }

            chatSession.IsAnalyticsRecorded = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Shared session analytics recorded for session '{SessionId}'. IsResolved={IsResolved}, MessageCount={MessageCount}.",
                    chatSession.SessionId,
                    isResolved,
                    prompts.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record shared session analytics for session '{SessionId}' (attempt {Attempt}).",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts);
        }
    }

    private async Task EvaluateConversionGoalsAsync(
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        AnalyticsMetadata analyticsMetadata,
        CancellationToken cancellationToken)
    {
        try
        {
            var goalResults = await _postSessionProcessingService.EvaluateConversionGoalsAsync(
                profile,
                prompts,
                analyticsMetadata.ConversionGoals,
                cancellationToken);

            if (goalResults is not null && goalResults.Count > 0)
            {
                foreach (var recorder in _conversionGoalRecorders)
                {
                    await recorder.RecordConversionGoalsAsync(profile, chatSession, goalResults, cancellationToken);
                }

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Shared conversion goals evaluated for session '{SessionId}': {GoalCount} goal(s), total score {Score}/{MaxScore}.",
                        chatSession.SessionId,
                        goalResults.Count,
                        goalResults.Sum(r => r.Score),
                        goalResults.Sum(r => r.MaxScore));
                }
            }
            else if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Shared conversion goals evaluation returned no results for session '{SessionId}'.",
                    chatSession.SessionId);
            }

            chatSession.IsConversionGoalsEvaluated = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to evaluate shared conversion goals for session '{SessionId}' (attempt {Attempt}).",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts);
        }
    }
}
