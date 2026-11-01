using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores the time-off ledger.
/// </summary>
internal sealed class TimeOffLedgerStore : DocumentCatalog<TimeOffLedgerEntry, TimeOffLedgerEntryIndex>
{
    public TimeOffLedgerStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <summary>
    /// Lists a worker's entries under a policy, oldest first. A worker has a few entries a month under a policy.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffLedgerEntry>> ListAsync(string workerId, string policyId, CancellationToken cancellationToken = default)
    {
        var entries = await Session.Query<TimeOffLedgerEntry, TimeOffLedgerEntryIndex>(index => index.WorkerId == workerId && index.PolicyId == policyId, collection: CollectionName)
            .OrderBy(index => index.EffectiveDate)
            .ThenBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return entries.ToArray();
    }

    public Task<TimeOffLedgerEntry> FindByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Session.Query<TimeOffLedgerEntry, TimeOffLedgerEntryIndex>(index => index.IdempotencyKey == idempotencyKey, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(int Count, IReadOnlyList<TimeOffLedgerEntry> Items)> PageAsync(string workerId, string policyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(policyId)
            ? Session.Query<TimeOffLedgerEntry, TimeOffLedgerEntryIndex>(index => index.WorkerId == workerId, collection: CollectionName)
            : Session.Query<TimeOffLedgerEntry, TimeOffLedgerEntryIndex>(index => index.WorkerId == workerId && index.PolicyId == policyId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.EffectiveDate).ThenByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
