using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Indexes;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.BackgroundTasks;

/// <summary>
/// Periodically closes inactive AI chat sessions and marks them for post-session processing.
/// Mirrors the behavior of Orchard Core's AIChatSessionCloseBackgroundTask.
/// </summary>
public sealed class AIChatSessionCloseBackgroundService : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _defaultInactivityTimeout = TimeSpan.FromMinutes(30);
    private static readonly int _maxRetryAttempts = 3;
    private static readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AIChatSessionCloseBackgroundService> _logger;

    public AIChatSessionCloseBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AIChatSessionCloseBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var session = scope.ServiceProvider.GetRequiredService<ISession>();
                var profileManager = scope.ServiceProvider.GetRequiredService<IAIProfileManager>();
                var utcNow = DateTime.UtcNow;

                await CloseInactiveSessionsAsync(session, profileManager, utcNow, stoppingToken);
                await RetryPendingProcessingAsync(session, utcNow, stoppingToken);

                await session.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while closing inactive AI chat sessions.");
            }
        }
    }

    /// <summary>
    /// Finds active sessions that have exceeded their profile's inactivity timeout and closes them.
    /// </summary>
    private async Task CloseInactiveSessionsAsync(
        ISession session,
        IAIProfileManager profileManager,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var profiles = await profileManager.GetAsync(AIProfileType.Chat);

        foreach (var profile in profiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var settings = profile.As<AIProfileDataExtractionSettings>();
            var timeout = settings?.SessionInactivityTimeoutInMinutes > 0
                ? TimeSpan.FromMinutes(settings.SessionInactivityTimeoutInMinutes)
                : _defaultInactivityTimeout;

            var cutoffUtc = utcNow - timeout;

            var inactiveSessions = await session
                .Query<AIChatSession, AIChatSessionIndex>(
                    i => i.ProfileId == profile.ItemId
                        && i.Status == (int)ChatSessionStatus.Active
                        && i.LastActivityUtc < cutoffUtc)
                .ListAsync(cancellationToken);

            foreach (var chatSession in inactiveSessions)
            {
                chatSession.Status = ChatSessionStatus.Closed;
                chatSession.ClosedAtUtc = utcNow;

                var hasPostProcessing = NeedsPostSessionProcessing(profile);
                if (hasPostProcessing)
                {
                    chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Pending;
                    chatSession.PostSessionProcessingAttempts = 0;
                    chatSession.PostSessionProcessingLastAttemptUtc = null;
                }
                else
                {
                    chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.None;
                }

                await session.SaveAsync(chatSession);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Closed inactive session '{SessionId}' for profile '{ProfileId}'. Post-processing: {NeedsProcessing}.",
                        chatSession.SessionId,
                        profile.ItemId,
                        hasPostProcessing);
                }
            }
        }
    }

    /// <summary>
    /// Retries post-session processing for sessions that are still pending and within the retry window.
    /// </summary>
    private async Task RetryPendingProcessingAsync(
        ISession session,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var pendingSessions = await session
            .Query<AIChatSession, AIChatSessionIndex>(
                i => i.Status == (int)ChatSessionStatus.Closed)
            .ListAsync(cancellationToken);

        foreach (var chatSession in pendingSessions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (chatSession.PostSessionProcessingStatus != PostSessionProcessingStatus.Pending)
            {
                continue;
            }

            if (chatSession.PostSessionProcessingAttempts >= _maxRetryAttempts)
            {
                chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Failed;
                await session.SaveAsync(chatSession);

                _logger.LogWarning(
                    "Post-session processing for session '{SessionId}' failed after {MaxAttempts} attempts.",
                    chatSession.SessionId,
                    _maxRetryAttempts);
                continue;
            }

            if (chatSession.PostSessionProcessingLastAttemptUtc.HasValue
                && (utcNow - chatSession.PostSessionProcessingLastAttemptUtc.Value) < _retryDelay)
            {
                continue;
            }

            chatSession.PostSessionProcessingAttempts++;
            chatSession.PostSessionProcessingLastAttemptUtc = utcNow;

            // Mark as completed since we don't have OC-specific post-session pipeline.
            // When a post-session processing service is registered at the framework level,
            // this should call it and only mark completed on success.
            chatSession.PostSessionProcessingStatus = PostSessionProcessingStatus.Completed;
            await session.SaveAsync(chatSession);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Marked post-session processing as completed for session '{SessionId}'.",
                    chatSession.SessionId);
            }
        }
    }

    /// <summary>
    /// Determines whether a profile has any post-session processing configured.
    /// </summary>
    private static bool NeedsPostSessionProcessing(AIProfile profile)
    {
        var extractionSettings = profile.As<AIProfileDataExtractionSettings>();
        if (extractionSettings?.EnableDataExtraction == true)
        {
            return true;
        }

        var analytics = profile.As<AnalyticsMetadata>();
        if (analytics is not null
            && (analytics.EnableSessionMetrics || analytics.EnableConversionMetrics || analytics.EnableAIResolutionDetection))
        {
            return true;
        }

        return false;
    }
}
