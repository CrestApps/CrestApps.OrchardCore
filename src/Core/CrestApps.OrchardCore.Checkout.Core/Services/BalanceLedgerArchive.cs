using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Models;
using CrestApps.OrchardCore.Payments;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Keeps every stored balance's document small, the same way for every kind of balance (gift cards, store credit and the
/// like): the balance keeps its latest changes and older ones move to their own indexed records, so reading and
/// rewriting the balance never grows with its history, and finding a change by its reference is a keyed lookup.
/// </summary>
public interface IBalanceLedgerArchive
{
    /// <summary>
    /// Moves the changes beyond the latest <see cref="BalanceLedger.RecentEntryCount"/> out of a balance's activity into
    /// records, in the same unit of work as the balance it is called for (call it from the store's save).
    /// </summary>
    /// <typeparam name="TEntry">The balance's entry type.</typeparam>
    /// <param name="ledger">The kind of balance, such as <c>StoreCredit</c>.</param>
    /// <param name="accountId">The balance document's id.</param>
    /// <param name="activity">The balance's activity, oldest first; older entries are removed from it.</param>
    /// <param name="reference">Reads an entry's reference, for a balance whose entries carry one.</param>
    Task ArchiveAsync<TEntry>(string ledger, string accountId, IList<TEntry> activity, Func<TEntry, string> reference = null)
        where TEntry : BalanceLedgerEntry;

    /// <summary>
    /// Returns whether a moved-out change of a balance has a reference.
    /// </summary>
    /// <param name="ledger">The kind of balance.</param>
    /// <param name="accountId">The balance document's id.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> HasReferenceAsync(string ledger, string accountId, string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the moved-out changes of a balance, oldest first.
    /// </summary>
    /// <param name="ledger">The kind of balance.</param>
    /// <param name="accountId">The balance document's id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<BalanceLedgerEntryRecord>> ListAsync(string ledger, string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a moved-out change, such as one whose reason was scrubbed for a personal data request.
    /// </summary>
    /// <param name="record">The record.</param>
    Task SaveAsync(BalanceLedgerEntryRecord record);
}

/// <summary>
/// The default <see cref="IBalanceLedgerArchive"/>, over the session of the request.
/// </summary>
public sealed class BalanceLedgerArchive : IBalanceLedgerArchive
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="BalanceLedgerArchive"/> class.
    /// </summary>
    /// <param name="session">The session the balance is saved in.</param>
    public BalanceLedgerArchive(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task ArchiveAsync<TEntry>(string ledger, string accountId, IList<TEntry> activity, Func<TEntry, string> reference = null)
        where TEntry : BalanceLedgerEntry
    {
        ArgumentException.ThrowIfNullOrEmpty(ledger);

        if (string.IsNullOrEmpty(accountId) || activity is null || activity.Count <= BalanceLedger.RecentEntryCount)
        {
            return;
        }

        var overflow = activity.Count - BalanceLedger.RecentEntryCount;

        for (var index = 0; index < overflow; index++)
        {
            var entry = activity[index];

            await _session.SaveAsync(new BalanceLedgerEntryRecord
            {
                Ledger = ledger,
                AccountId = accountId,
                CreatedUtc = entry.CreatedUtc,
                Amount = entry.Amount,
                Balance = entry.Balance,
                Reason = entry.Reason,
                SessionId = entry.SessionId,
                Actor = entry.Actor,
                Reference = reference?.Invoke(entry),
            });
        }

        for (var index = 0; index < overflow; index++)
        {
            activity.RemoveAt(0);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasReferenceAsync(string ledger, string accountId, string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(reference))
        {
            return false;
        }

        var cut = BalanceLedgerEntryIndexProvider.Cut(reference);

        var candidates = await _session
            .Query<BalanceLedgerEntryRecord, BalanceLedgerEntryIndex>(index => index.Ledger == ledger && index.AccountId == accountId && index.Reference == cut)
            .ListAsync(cancellationToken);

        // The index keeps the reference cut to its column; the record keeps it whole.
        return candidates.Any(record => string.Equals(record.Reference, reference, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BalanceLedgerEntryRecord>> ListAsync(string ledger, string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return [];
        }

        var records = await _session
            .Query<BalanceLedgerEntryRecord, BalanceLedgerEntryIndex>(index => index.Ledger == ledger && index.AccountId == accountId)
            .OrderBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return [.. records];
    }

    /// <inheritdoc/>
    public Task SaveAsync(BalanceLedgerEntryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return _session.SaveAsync(record);
    }
}
