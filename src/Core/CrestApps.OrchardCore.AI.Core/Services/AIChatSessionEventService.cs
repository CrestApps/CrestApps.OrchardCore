using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Manages the recording and updating of chat session analytics events.
/// </summary>
public sealed class AIChatSessionEventService
{
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;

    public AIChatSessionEventService(
        ISession session,
        TimeProvider timeProvider)
    {
        _session = session;
        _timeProvider = timeProvider;
    }
    /// <summary>
    /// Records a new session event when a chat session starts.
    /// Uses the session's UserId or ClientId as the visitor identifier.
    /// </summary>
    public async Task RecordSessionStartedAsync(AIChatSession chatSession)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var isAuthenticated = !string.IsNullOrEmpty(chatSession.UserId);

        var evt = new AIChatSessionEvent
        {
            SessionId = chatSession.SessionId,
            ProfileId = chatSession.ProfileId,
            VisitorId = isAuthenticated ? chatSession.UserId : chatSession.ClientId ?? string.Empty,
            UserId = chatSession.UserId,
            IsAuthenticated = isAuthenticated,
            SessionStartedUtc = now,
            MessageCount = 0,
            HandleTimeSeconds = 0,
            IsResolved = false,
            CompletionCount = 0,
            CreatedUtc = now,
        };

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }
    /// <summary>
    /// Updates the session event when a chat session ends.
    /// </summary>
    public async Task RecordSessionEndedAsync(AIChatSession chatSession, int promptCount, bool isResolved)
    {
        var evt = await FindEventBySessionIdAsync(chatSession.SessionId);

        if (evt is null)
        {
            // If no start event exists, create a complete record.
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var isAuthenticated = !string.IsNullOrEmpty(chatSession.UserId);

            evt = new AIChatSessionEvent
            {
                SessionId = chatSession.SessionId,
                ProfileId = chatSession.ProfileId,
                VisitorId = isAuthenticated ? chatSession.UserId : chatSession.ClientId ?? string.Empty,
                UserId = chatSession.UserId,
                IsAuthenticated = isAuthenticated,
                SessionStartedUtc = chatSession.CreatedUtc,
                SessionEndedUtc = chatSession.ClosedAtUtc ?? now,
                MessageCount = promptCount,
                HandleTimeSeconds = ((chatSession.ClosedAtUtc ?? now) - chatSession.CreatedUtc).TotalSeconds,
                IsResolved = isResolved,
                CompletionCount = 0,
                CreatedUtc = now,
            };

            await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
            return;
        }

        var endTime = chatSession.ClosedAtUtc ?? _timeProvider.GetUtcNow().UtcDateTime;

        evt.SessionEndedUtc = endTime;
        evt.MessageCount = promptCount;
        evt.IsResolved = isResolved;
        evt.HandleTimeSeconds = (endTime - evt.SessionStartedUtc).TotalSeconds;

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }
    /// <summary>
    /// Accumulates token usage and response latency metrics for the session.
    /// Called after each message completion to update running totals.
    /// </summary>
    public async Task RecordCompletionUsageAsync(string sessionId, int inputTokens, int outputTokens)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.TotalInputTokens += inputTokens;
        evt.TotalOutputTokens += outputTokens;

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }

    public async Task RecordResponseLatencyAsync(string sessionId, double responseLatencyMs)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null || responseLatencyMs <= 0)
        {
            return;
        }

        evt.CompletionCount++;
        evt.AverageResponseLatencyMs =
            ((evt.AverageResponseLatencyMs * (evt.CompletionCount - 1)) + responseLatencyMs) / evt.CompletionCount;

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }
    /// <summary>
    /// Updates the resolution status for a session based on AI analysis.
    /// </summary>
    public async Task UpdateResolutionStatusAsync(string sessionId, bool isResolved)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.IsResolved = isResolved;

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }
    /// <summary>
    /// Records conversion goal evaluation results for a session.
    /// </summary>
    public async Task RecordConversionMetricsAsync(string sessionId, List<ConversionGoalResult> goalResults)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.ConversionGoalResults = goalResults;
        evt.ConversionScore = goalResults.Sum(r => r.Score);
        evt.ConversionMaxScore = goalResults.Sum(r => r.MaxScore);

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }
    /// <summary>
    /// Records the user's feedback rating counts for a session.
    /// </summary>
    public async Task RecordUserRatingAsync(string sessionId, int thumbsUpCount, int thumbsDownCount)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.ThumbsUpCount = thumbsUpCount;
        evt.ThumbsDownCount = thumbsDownCount;

        // Keep legacy UserRating field in sync for backward compatibility.
        if (thumbsUpCount + thumbsDownCount > 0)
        {
            evt.UserRating = thumbsUpCount >= thumbsDownCount;
        }
        else
        {
            evt.UserRating = null;
        }

        await _session.SaveAsync(evt, collection: AIConstants.AICollectionName);
    }

    private async Task<AIChatSessionEvent> FindEventBySessionIdAsync(string sessionId)
    {
        return await _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>(
            i => i.SessionId == sessionId,
            collection: AIConstants.AICollectionName)
                .FirstOrDefaultAsync();
    }
}
