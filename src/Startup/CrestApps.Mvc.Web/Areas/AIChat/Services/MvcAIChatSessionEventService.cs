using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.AIChat.Indexes;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatSessionEventService
{
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;

    public MvcAIChatSessionEventService(
        ISession session,
        TimeProvider timeProvider)
    {
        _session = session;
        _timeProvider = timeProvider;
    }

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
            CreatedUtc = now,
        };

        await _session.SaveAsync(evt);
    }

    public async Task RecordSessionEndedAsync(AIChatSession chatSession, int promptCount, bool isResolved)
    {
        var evt = await FindEventBySessionIdAsync(chatSession.SessionId);

        if (evt is null)
        {
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
                CreatedUtc = now,
            };

            await _session.SaveAsync(evt);
            return;
        }

        var endTime = chatSession.ClosedAtUtc ?? _timeProvider.GetUtcNow().UtcDateTime;

        evt.SessionEndedUtc = endTime;
        evt.MessageCount = promptCount;
        evt.IsResolved = isResolved;
        evt.HandleTimeSeconds = (endTime - evt.SessionStartedUtc).TotalSeconds;

        await _session.SaveAsync(evt);
    }

    public async Task RecordCompletionMetricsAsync(string sessionId, int inputTokens, int outputTokens, double responseLatencyMs)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.TotalInputTokens += inputTokens;
        evt.TotalOutputTokens += outputTokens;

        var completionCount = evt.MessageCount > 0 ? evt.MessageCount : 1;
        evt.AverageResponseLatencyMs =
            ((evt.AverageResponseLatencyMs * (completionCount - 1)) + responseLatencyMs) / completionCount;

        await _session.SaveAsync(evt);
    }

    public async Task RecordConversionMetricsAsync(string sessionId, List<ConversionGoalResult> goalResults)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.ConversionGoalResults = goalResults;
        evt.ConversionScore = goalResults.Sum(result => result.Score);
        evt.ConversionMaxScore = goalResults.Sum(result => result.MaxScore);

        await _session.SaveAsync(evt);
    }

    public async Task RecordUserRatingAsync(string sessionId, int thumbsUpCount, int thumbsDownCount)
    {
        var evt = await FindEventBySessionIdAsync(sessionId);

        if (evt is null)
        {
            return;
        }

        evt.ThumbsUpCount = thumbsUpCount;
        evt.ThumbsDownCount = thumbsDownCount;
        evt.UserRating = thumbsUpCount + thumbsDownCount > 0
            ? thumbsUpCount >= thumbsDownCount
            : null;

        await _session.SaveAsync(evt);
    }

    public async Task<IReadOnlyList<AIChatSessionEvent>> GetAsync(
        string profileId,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>();

        if (!string.IsNullOrEmpty(profileId))
        {
            query = query.Where(x => x.ProfileId == profileId);
        }

        if (startDateUtc.HasValue)
        {
            var start = startDateUtc.Value.Date;
            query = query.Where(x => x.SessionStartedUtc >= start);
        }

        if (endDateUtc.HasValue)
        {
            var endExclusive = endDateUtc.Value.Date.AddDays(1);
            query = query.Where(x => x.SessionStartedUtc < endExclusive);
        }

        var events = await query.ListAsync(cancellationToken);
        return events.OrderByDescending(x => x.SessionStartedUtc).ToList();
    }

    private async Task<AIChatSessionEvent> FindEventBySessionIdAsync(string sessionId) =>
        await _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>(x => x.SessionId == sessionId)
            .FirstOrDefaultAsync();
}
