using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Manages the recording and updating of chat session analytics events.
/// </summary>
public sealed class AIChatSessionEventService
{
    private readonly ISession _session;
    private readonly IClock _clock;

    public AIChatSessionEventService(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// Records a new session event when a chat session starts.
    /// Uses the session's UserId or ClientId as the visitor identifier.
    /// </summary>
    public async Task RecordSessionStartedAsync(AIChatSession chatSession)
    {
        var now = _clock.UtcNow;
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
            CreatedUtc = now,
        };

        await _session.SaveAsync(evt, collection: AIConstants.CollectionName);
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
            var now = _clock.UtcNow;
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
                CreatedUtc = now,
            };

            await _session.SaveAsync(evt, collection: AIConstants.CollectionName);
            return;
        }

        var endTime = chatSession.ClosedAtUtc ?? _clock.UtcNow;

        evt.SessionEndedUtc = endTime;
        evt.MessageCount = promptCount;
        evt.IsResolved = isResolved;
        evt.HandleTimeSeconds = (endTime - evt.SessionStartedUtc).TotalSeconds;

        await _session.SaveAsync(evt, collection: AIConstants.CollectionName);
    }

    /// <summary>
    /// Accumulates token usage and response latency metrics for the session.
    /// Called after each message completion to update running totals.
    /// </summary>
    public async Task RecordCompletionMetricsAsync(string sessionId, int inputTokens, int outputTokens, double responseLatencyMs)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.TotalInputTokens += inputTokens;
        evt.TotalOutputTokens += outputTokens;

        // Compute running average latency.
        var completionCount = evt.MessageCount > 0 ? evt.MessageCount : 1;
        evt.AverageResponseLatencyMs =
            ((evt.AverageResponseLatencyMs * (completionCount - 1)) + responseLatencyMs) / completionCount;

        await _session.SaveAsync(evt, collection: AIConstants.CollectionName);
    }

    /// <summary>
    /// Records the user's feedback rating for a session.
    /// </summary>
    public async Task RecordUserRatingAsync(string sessionId, bool isPositive)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.UserRating = isPositive;

        await _session.SaveAsync(evt, collection: AIConstants.CollectionName);
    }

    private async Task<AIChatSessionEvent> FindEventBySessionIdAsync(string sessionId)
    {
        return await _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>(
                i => i.SessionId == sessionId,
                collection: AIConstants.CollectionName)
            .FirstOrDefaultAsync();
    }
}
