using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Indexes;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatSessionExtractedDataService : IAIChatSessionExtractedDataRecorder
{
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;

    public MvcAIChatSessionExtractedDataService(
        ISession session,
        TimeProvider timeProvider)
    {
        _session = session;
        _timeProvider = timeProvider;
    }

    public async Task RecordExtractedDataAsync(
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindBySessionIdAsync(session.SessionId);

        if (session.ExtractedData.Count == 0)
        {
            if (existing is not null)
            {
                _session.Delete(existing);
            }

            return;
        }

        var record = existing ?? new AIChatSessionExtractedDataRecord
        {
            ItemId = session.SessionId,
            SessionId = session.SessionId,
        };

        record.ProfileId = profile.ItemId;
        record.SessionStartedUtc = session.CreatedUtc;
        record.SessionEndedUtc = session.ClosedAtUtc;
        record.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        record.Values = session.ExtractedData
            .Where(pair => pair.Value.Values.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Values.ToList(),
                StringComparer.OrdinalIgnoreCase);

        await _session.SaveAsync(record);
    }

    public async Task<IReadOnlyList<AIChatSessionExtractedDataRecord>> GetAsync(
        string profileId,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AIChatSessionExtractedDataRecord, AIChatSessionExtractedDataIndex>(x => x.ProfileId == profileId);

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

        var records = await query.ListAsync(cancellationToken);
        return records.OrderByDescending(x => x.SessionStartedUtc).ToList();
    }

    private async Task<AIChatSessionExtractedDataRecord> FindBySessionIdAsync(string sessionId) =>
        await _session.Query<AIChatSessionExtractedDataRecord, AIChatSessionExtractedDataIndex>(x => x.SessionId == sessionId)
            .FirstOrDefaultAsync();
}
