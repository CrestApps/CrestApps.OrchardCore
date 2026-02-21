using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
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
                    i => i.ProfileId == profile.ItemId,
                    collection: AIConstants.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var chatSession in inactiveSessions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (chatSession.Status != ChatSessionStatus.Active)
                {
                    continue;
                }

                if (chatSession.LastActivityUtc >= cutoffUtc)
                {
                    continue;
                }

                chatSession.Status = ChatSessionStatus.Closed;
                chatSession.ClosedAtUtc = utcNow;

                await session.SaveAsync(chatSession, false, collection: AIConstants.CollectionName, cancellationToken);

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Closed inactive AI chat session '{SessionId}' for profile '{ProfileId}'.", chatSession.SessionId, profile.ItemId);
                }

                // Trigger workflow event if workflows are available.
                try
                {
                    var workflowManager = serviceProvider.GetService<IWorkflowManager>();

                    if (workflowManager != null)
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
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to trigger AIChatSessionClosedEvent for session '{SessionId}'.", chatSession.SessionId);
                }
            }
        }
    }
}
