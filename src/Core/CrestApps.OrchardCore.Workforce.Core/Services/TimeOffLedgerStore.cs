using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Names the time-off ledger among the quantity ledgers.
/// </summary>
public sealed class TimeOffLedger
{
}

/// <summary>
/// The time-off ledger: a quantity ledger with one balance per worker and policy, read and written as time-off
/// entries.
/// </summary>
internal sealed class TimeOffLedgerStore
{
    private readonly IQuantityLedger<TimeOffLedger> _ledger;

    public TimeOffLedgerStore(IQuantityLedger<TimeOffLedger> ledger)
    {
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<TimeOffLedgerEntry>> ListAsync(string workerId, string policyId, CancellationToken cancellationToken = default)
        => (await _ledger.ListAsync(workerId, policyId, cancellationToken)).Select(TimeOffLedgerMapping.ToTimeOff).ToArray();

    public async Task<IReadOnlyList<TimeOffLedgerEntry>> ListGrantsAsync(CancellationToken cancellationToken = default)
        => (await _ledger.ListByKindAsync(nameof(TimeOffLedgerKind.Grant), cancellationToken)).Select(TimeOffLedgerMapping.ToTimeOff).ToArray();

    public async Task<TimeOffLedgerEntry> FindByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _ledger.FindByKeyAsync(idempotencyKey, cancellationToken) is { } entry ? TimeOffLedgerMapping.ToTimeOff(entry) : null;

    public async Task CreateAsync(TimeOffLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var posted = await _ledger.PostAsync(TimeOffLedgerMapping.ToQuantity(entry), cancellationToken);

        entry.ItemId = posted.Entry.ItemId;
        entry.CreatedUtc = posted.Entry.CreatedUtc;
    }

    public async Task<(int Count, IReadOnlyList<TimeOffLedgerEntry> Items)> PageAsync(string workerId, string policyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var (count, items) = await _ledger.PageAsync(workerId, policyId, skip, take, cancellationToken);

        return (count, items.Select(TimeOffLedgerMapping.ToTimeOff).ToArray());
    }
}

/// <summary>
/// Maps time-off entries to and from quantity ledger entries: the worker is the account and the policy the bucket.
/// </summary>
internal static class TimeOffLedgerMapping
{
    public static QuantityLedgerEntry ToQuantity(TimeOffLedgerEntry entry) => new()
    {
        ItemId = entry.ItemId,
        AccountId = entry.WorkerId,
        Bucket = entry.PolicyId,
        Kind = entry.Kind.ToString(),
        Amount = entry.Amount,
        EffectiveDate = entry.EffectiveDate,
        ExpiresOn = entry.ExpiresOn,
        SourceRef = entry.SourceRef,
        IdempotencyKey = entry.IdempotencyKey,
        Actor = entry.Actor,
        Note = entry.Note,
        CreatedUtc = entry.CreatedUtc,
        Properties = entry.Properties,
    };

    public static TimeOffLedgerEntry ToTimeOff(QuantityLedgerEntry entry) => new()
    {
        ItemId = entry.ItemId,
        WorkerId = entry.AccountId,
        PolicyId = entry.Bucket,
        Kind = Enum.TryParse<TimeOffLedgerKind>(entry.Kind, out var kind) ? kind : TimeOffLedgerKind.Adjustment,
        Amount = entry.Amount,
        EffectiveDate = entry.EffectiveDate,
        ExpiresOn = entry.ExpiresOn,
        SourceRef = entry.SourceRef,
        IdempotencyKey = entry.IdempotencyKey,
        Actor = entry.Actor,
        Note = entry.Note,
        CreatedUtc = entry.CreatedUtc,
        Properties = entry.Properties,
    };
}
