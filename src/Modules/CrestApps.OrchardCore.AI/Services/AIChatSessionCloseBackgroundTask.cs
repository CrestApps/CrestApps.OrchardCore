using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Workflows.Services;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Background task that periodically closes inactive AI chat sessions, retries pending post-close processing, and triggers workflow events.
/// </summary>
[BackgroundTask(
    Title = "AI Chat Session Close",
    Schedule = "*/10 * * * *",
    Description = "Periodically closes inactive AI chat sessions, retries pending post-close processing, and triggers workflow events.",
    LockTimeout = 5_000,
    LockExpiration = 30_000)]

/// <summary>
/// Represents the AI chat session close background task.
/// </summary>
public sealed class AIChatSessionCloseBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _defaultInactivityTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Executes the background work to close inactive sessions and retry pending post-close processing.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var promptStore = serviceProvider.GetRequiredService<IAIChatSessionPromptStore>();
        var postCloseProcessor = serviceProvider.GetRequiredService<AIChatSessionPostCloseProcessor>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIChatSessionCloseBackgroundTask>>();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var profiles = await profileManager.GetAsync(AIProfileType.Chat, cancellationToken);

        foreach (var profile in profiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await CloseInactiveSessionsAsync(serviceProvider, session, promptStore, postCloseProcessor, profile, utcNow, logger, cancellationToken);
            await RetryPendingProcessingAsync(serviceProvider, session, promptStore, postCloseProcessor, profile, utcNow, logger, cancellationToken);
        }
    }

    private static async Task CloseInactiveSessionsAsync(
        IServiceProvider serviceProvider,
        ISession session,
        IAIChatSessionPromptStore promptStore,
        AIChatSessionPostCloseProcessor postCloseProcessor,
        AIProfile profile,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = utcNow - GetInactivityTimeout(profile);

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

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
            chatSession.Status = DetermineInactiveSessionStatus(prompts);
            chatSession.ClosedAtUtc = utcNow;

            var result = await RunPostCloseProcessingAsync(postCloseProcessor, profile, chatSession, prompts, logger, cancellationToken);

            await session.SaveAsync(chatSession, false, collection: AIConstants.AICollectionName, cancellationToken);

            await TriggerSessionClosedWorkflowAsync(serviceProvider, profile, chatSession, logger);

            if (result.PostSessionTasksCompletedNow)
            {
                await TriggerPostProcessedWorkflowAsync(serviceProvider, profile, chatSession, logger);
            }
        }
    }

    private static async Task RetryPendingProcessingAsync(
        IServiceProvider serviceProvider,
        ISession session,
        IAIChatSessionPromptStore promptStore,
        AIChatSessionPostCloseProcessor postCloseProcessor,
        AIProfile profile,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pendingSessions = (await session.Query<AIChatSession, AIChatSessionIndex>(
            i => i.ProfileId == profile.ItemId
                && (i.Status == ChatSessionStatus.Closed || i.Status == ChatSessionStatus.Abandoned),
            collection: AIConstants.AICollectionName)
            .ListAsync(cancellationToken))
            .Where(chatSession => chatSession.PostSessionProcessingStatus == PostSessionProcessingStatus.Pending)
            .ToArray();

        foreach (var chatSession in pendingSessions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (chatSession.PostSessionProcessingAttempts >= AIChatSessionPostCloseProcessor.MaxPostCloseAttempts)
            {
                chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Failed;
                await session.SaveAsync(chatSession, false, collection: AIConstants.AICollectionName, cancellationToken);

                logger.LogWarning(
                    "Post-close processing for session '{SessionId}' exceeded the maximum number of attempts ({MaxAttempts}).",
                    chatSession.SessionId,
                    AIChatSessionPostCloseProcessor.MaxPostCloseAttempts);
                continue;
            }

            if (chatSession.PostSessionProcessingLastAttemptUtc.HasValue
                && utcNow - chatSession.PostSessionProcessingLastAttemptUtc.Value < _retryDelay)
            {
                continue;
            }

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
            var result = await RunPostCloseProcessingAsync(postCloseProcessor, profile, chatSession, prompts, logger, cancellationToken);

            await session.SaveAsync(chatSession, false, collection: AIConstants.AICollectionName, cancellationToken);

            if (result.PostSessionTasksCompletedNow)
            {
                await TriggerPostProcessedWorkflowAsync(serviceProvider, profile, chatSession, logger);
            }
        }
    }

    private static async Task<AIChatSessionPostCloseProcessingResult> RunPostCloseProcessingAsync(
        AIChatSessionPostCloseProcessor postCloseProcessor,
        AIProfile profile,
        AIChatSession chatSession,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!AIChatSessionPostCloseProcessor.NeedsProcessing(profile, chatSession))
        {
            chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.None;
            return new AIChatSessionPostCloseProcessingResult { IsCompleted = true };
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Running shared post-close processing for session '{SessionId}' (attempt {Attempt}).",
                chatSession.SessionId,
                chatSession.PostSessionProcessingAttempts + 1);
        }

        return await postCloseProcessor.ProcessAsync(profile, chatSession, prompts, cancellationToken);
    }

    private static TimeSpan GetInactivityTimeout(AIProfile profile)
    {
        var settings = profile.GetSettings<AIProfileDataExtractionSettings>();
        return settings.SessionInactivityTimeoutInMinutes > 0
            ? TimeSpan.FromMinutes(settings.SessionInactivityTimeoutInMinutes)
            : _defaultInactivityTimeout;
    }

    private static ChatSessionStatus DetermineInactiveSessionStatus(IReadOnlyList<AIChatSessionPrompt> prompts)
    {
        return prompts.Any(prompt => prompt.Role == ChatRole.User)
            ? ChatSessionStatus.Closed
            : ChatSessionStatus.Abandoned;
    }

    private static async Task TriggerSessionClosedWorkflowAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
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
                { "ClosedAtUtc", chatSession.ClosedAtUtc },
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

    private static async Task TriggerPostProcessedWorkflowAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
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
                { "Results", chatSession.PostSessionResults },
            };

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionPostProcessedEvent),
                input,
                correlationId: chatSession.SessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger AIChatSessionPostProcessedEvent for session '{SessionId}'.", chatSession.SessionId);
        }
    }
}
