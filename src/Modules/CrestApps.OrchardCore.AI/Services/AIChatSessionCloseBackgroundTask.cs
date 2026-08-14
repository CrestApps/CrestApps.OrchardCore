using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Background task that periodically closes inactive AI chat sessions, retries
/// pending post-close processing, and triggers workflow events.
/// Uses <see cref="IAIChatSessionStore"/> for unscoped data access (no HTTP context dependency),
/// and <see cref="AIChatSessionPostCloseProcessor"/> from the framework for post-close logic.
/// </summary>
[BackgroundTask(
    Title = "AI Chat Session Close",
    Schedule = "*/10 * * * *",
    Description = "Periodically closes inactive AI chat sessions, retries pending post-close processing, and triggers workflow events.",
    LockTimeout = 5_000,
    LockExpiration = 30_000)]
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
        var clock = serviceProvider.GetRequiredService<IClock>();
        var sessionStore = serviceProvider.GetRequiredService<IAIChatSessionStore>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
        var promptStore = serviceProvider.GetRequiredService<IAIChatSessionPromptStore>();
        var postCloseProcessor = serviceProvider.GetRequiredService<AIChatSessionPostCloseProcessor>();
        var storeCommitter = serviceProvider.GetRequiredService<IStoreCommitter>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIChatSessionCloseBackgroundTask>>();

        var utcNow = clock.UtcNow;
        var profiles = await profileManager.GetAsync(AIProfileType.Chat, cancellationToken);

        foreach (var profile in profiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await CloseInactiveSessionsAsync(serviceProvider, sessionStore, promptStore, postCloseProcessor, profile, utcNow, logger, cancellationToken);
            await RetryPendingProcessingAsync(serviceProvider, sessionStore, promptStore, postCloseProcessor, profile, utcNow, logger, cancellationToken);
        }

        await storeCommitter.CommitAsync(cancellationToken);
    }

    private static async Task CloseInactiveSessionsAsync(
        IServiceProvider serviceProvider,
        IAIChatSessionStore sessionStore,
        IAIChatSessionPromptStore promptStore,
        AIChatSessionPostCloseProcessor postCloseProcessor,
        AIProfile profile,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = utcNow - GetInactivityTimeout(profile);
        var inactiveSessions = await sessionStore.GetInactiveActiveSessionsAsync(profile.ItemId, cutoffUtc, cancellationToken);

        foreach (var chatSession in inactiveSessions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
            chatSession.Status = DetermineInactiveSessionStatus(prompts);
            chatSession.ClosedAtUtc = utcNow;

            if (postCloseProcessor.QueueIfNeeded(profile, chatSession))
            {
                await postCloseProcessor.ProcessAsync(profile, chatSession, prompts, cancellationToken);
            }
            else
            {
                chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.None;
            }

            await sessionStore.SaveAsync(chatSession, cancellationToken);

            await TriggerSessionClosedWorkflowAsync(serviceProvider, profile, chatSession, logger);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Finalized inactive session '{SessionId}' for profile '{ProfileId}' as '{Status}'.",
                    chatSession.SessionId,
                    profile.ItemId,
                    chatSession.Status);
            }
        }
    }

    private static async Task RetryPendingProcessingAsync(
        IServiceProvider serviceProvider,
        IAIChatSessionStore sessionStore,
        IAIChatSessionPromptStore promptStore,
        AIChatSessionPostCloseProcessor postCloseProcessor,
        AIProfile profile,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var closedSessions = await sessionStore.GetClosedSessionsAsync(profile.ItemId, cancellationToken);

        var pendingSessions = closedSessions
            .Where(s => postCloseProcessor.NeedsProcessing(profile, s))
            .ToArray();

        foreach (var chatSession in pendingSessions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (chatSession.PostSessionProcessingAttempts >= postCloseProcessor.MaxPostCloseAttempts)
            {
                chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Failed;
                await sessionStore.SaveAsync(chatSession, cancellationToken);

                logger.LogWarning(
                    "Post-close processing for session '{SessionId}' exceeded the maximum number of attempts ({MaxAttempts}).",
                    chatSession.SessionId,
                    postCloseProcessor.MaxPostCloseAttempts);

                continue;
            }

            if (chatSession.PostSessionProcessingLastAttemptUtc.HasValue
                && utcNow - chatSession.PostSessionProcessingLastAttemptUtc.Value < _retryDelay)
            {
                continue;
            }

            var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);
            await postCloseProcessor.ProcessAsync(profile, chatSession, prompts, cancellationToken);
            await sessionStore.SaveAsync(chatSession, cancellationToken);

            if (chatSession.PostSessionProcessingStatus == PostSessionProcessingStatus.Completed)
            {
                await TriggerPostProcessedWorkflowAsync(serviceProvider, profile, chatSession, logger);
            }
        }
    }

    private static TimeSpan GetInactivityTimeout(AIProfile profile)
    {
        var settings = profile.GetOrCreateSettings<AIProfileDataExtractionSettings>();

        return settings?.SessionInactivityTimeoutInMinutes > 0
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
