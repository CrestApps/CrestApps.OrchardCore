using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

public sealed class CallQualityRecordStore : DocumentCatalog<CallQualityRecord, CallQualityRecordIndex>, ICallQualityRecordStore
{
    public CallQualityRecordStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    public async Task<CallQualityRecord> FindByRecordKeyAsync(string recordKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordKey);

        return await Session.Query<CallQualityRecord, CallQualityRecordIndex>(
            index => index.RecordKey == recordKey,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CallQualityRecord>> GetObservedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<CallQualityRecord, CallQualityRecordIndex>(
            index => index.ObservedUtc >= fromUtc && index.ObservedUtc < toUtc,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.ObservedUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    public async Task<IReadOnlyCollection<CallQualityRecord>> GetRecentForAgentAsync(string agentId, DateTime sinceUtc, int count, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var records = await Session.Query<CallQualityRecord, CallQualityRecordIndex>(
            index => index.AgentId == agentId && index.ObservedUtc >= sinceUtc,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.ObservedUtc)
            .Take(count)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }
}
