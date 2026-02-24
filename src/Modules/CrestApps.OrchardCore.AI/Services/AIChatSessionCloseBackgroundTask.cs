using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Services;

[BackgroundTask(
    Title = "AI Chat Session Close",
    Schedule = "*/10 * * * *",
    Description = "Periodically closes inactive AI chat sessions and triggers workflow events.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class AIChatSessionCloseBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var profileManager = serviceProvider.GetRequiredService<IAIProfileManager>();
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

            var settings = profile.GetSettings<AIProfileDataExtractionSettings>();

            if (!settings.EnableDataExtraction || settings.SessionInactivityTimeoutInMinutes <= 0)
            {
                continue;
            }

            var timeout = TimeSpan.FromMinutes(settings.SessionInactivityTimeoutInMinutes);
            var cutoffUtc = utcNow - timeout;

            // Query active sessions that are past the inactivity timeout.
            var inactiveSessions = await session.Query<AIChatSession, AIChatSessionIndex>(
                    i => i.ProfileId == profile.ItemId && i.Status == ChatSessionStatus.Active && i.LastActivityUtc < cutoffUtc,
                    collection: AIConstants.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var chatSession in inactiveSessions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                chatSession.Status = ChatSessionStatus.Closed;
                chatSession.ClosedAtUtc = utcNow;

                // Run post-session processing if configured.
                await RunPostSessionProcessingAsync(serviceProvider, profile, chatSession, logger, cancellationToken);

                // Record analytics event for abandoned session (inactivity = not resolved).
                await RecordAnalyticsEventAsync(serviceProvider, profile, chatSession, logger);

                await session.SaveAsync(chatSession, false, collection: AIConstants.CollectionName, cancellationToken);

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Closed inactive AI chat session '{SessionId}' for profile '{ProfileId}'.", chatSession.SessionId, profile.ItemId);
                }

                var workflowManager = serviceProvider.GetService<IWorkflowManager>();

                if (workflowManager != null)
                {
                    // Trigger workflow event if workflows are available.
                    try
                    {
                        var input = new Dictionary<string, object>
                        {
                            { "SessionId", chatSession.SessionId },
                            { "ProfileId", profile.ItemId },
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
        }
    }

    private static async Task RunPostSessionProcessingAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var postSessionService = serviceProvider.GetService<PostSessionProcessingService>();

            if (postSessionService is null)
            {
                return;
            }

            var results = await postSessionService.ProcessAsync(profile, chatSession, cancellationToken);

            if (results is null || results.Count == 0)
            {
                return;
            }

            chatSession.PostSessionResults = results;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Post-session processing completed for session '{SessionId}' with {TaskCount} results.",
                    chatSession.SessionId,
                    results.Count);
            }

            var workflowManager = serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager is not null)
            {
                var input = new Dictionary<string, object>
                {
                    { "SessionId", chatSession.SessionId },
                    { "ProfileId", profile.ItemId },
                    { "Results", results },
                };

                await workflowManager.TriggerEventAsync(
                    nameof(AIChatSessionPostProcessedEvent),
                    input,
                    correlationId: chatSession.SessionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Post-session processing failed for session '{SessionId}'.", chatSession.SessionId);
        }
    }

    private static async Task RecordAnalyticsEventAsync(
        IServiceProvider serviceProvider,
        AIProfile profile,
        AIChatSession chatSession,
        ILogger logger)
    {
        if (!profile.As<AIProfileAnalyticsMetadata>().EnableSessionMetrics)
        {
            return;
        }

        try
        {
            var eventService = serviceProvider.GetService<AIChatSessionEventService>();

            if (eventService is null)
            {
                return;
            }

            // Inactivity timeout = abandoned (not resolved).
            await eventService.RecordSessionEndedAsync(chatSession, isResolved: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record analytics event for abandoned session '{SessionId}'.", chatSession.SessionId);
        }
    }
}
