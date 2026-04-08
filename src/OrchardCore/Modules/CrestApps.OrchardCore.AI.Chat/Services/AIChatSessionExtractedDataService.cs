using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Indexes;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Modules;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class AIChatSessionExtractedDataService : IAIChatSessionExtractedDataRecorder
{
    private readonly ISession _session;
    private readonly IClock _clock;

    public AIChatSessionExtractedDataService(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
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
        record.UpdatedUtc = _clock.UtcNow;
        record.Values = session.ExtractedData
            .Where(pair => pair.Value.Values.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Values.ToList(),
                StringComparer.OrdinalIgnoreCase);

        await _session.SaveAsync(record, collection: AIConstants.AICollectionName, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AIChatSessionExtractedDataRecord>> GetAsync(
        string profileId,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AIChatSessionExtractedDataRecord, AIChatSessionExtractedDataIndex>(
            index => index.ProfileId == profileId,
            collection: AIConstants.AICollectionName);

        if (startDateUtc.HasValue)
        {
            var start = startDateUtc.Value.Date;
            query = query.Where(index => index.SessionStartedUtc >= start);
        }

        if (endDateUtc.HasValue)
        {
            var endExclusive = endDateUtc.Value.Date.AddDays(1);
            query = query.Where(index => index.SessionStartedUtc < endExclusive);
        }

        var records = await query.ListAsync(cancellationToken);
        return records.OrderByDescending(record => record.SessionStartedUtc).ToList();
    }

    private async Task<AIChatSessionExtractedDataRecord> FindBySessionIdAsync(string sessionId) =>
        await _session.Query<AIChatSessionExtractedDataRecord, AIChatSessionExtractedDataIndex>(
            index => index.SessionId == sessionId,
            collection: AIConstants.AICollectionName)
            .FirstOrDefaultAsync();
}
