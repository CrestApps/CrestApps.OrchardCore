namespace CrestApps.OrchardCore.Ledgers;

/// <summary>
/// A ledger of quantities with dated, expiring entries posted once by key, kept in a collection of its own. Register
/// one with <c>services.AddQuantityLedger&lt;TLedger&gt;(options =&gt; options.CollectionName = ...)</c>, where
/// <typeparamref name="TLedger"/> is a marker type of the owner, and create its index table with
/// <c>QuantityLedgerIndexSchema.CreateAsync</c>.
/// </summary>
/// <typeparam name="TLedger">The marker type that names the ledger.</typeparam>
public interface IQuantityLedger<TLedger>
{
    /// <summary>
    /// Posts an entry, or returns the entry already posted with the same idempotency key.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry kept, and whether it was posted now.</returns>
    Task<QuantityLedgerPostResult> PostAsync(QuantityLedgerEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the entry posted with an idempotency key.
    /// </summary>
    Task<QuantityLedgerEntry> FindByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the entries of one balance, oldest first.
    /// </summary>
    Task<IReadOnlyList<QuantityLedgerEntry>> ListAsync(string accountId, string bucket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the entries of every balance of an account, oldest first.
    /// </summary>
    Task<IReadOnlyList<QuantityLedgerEntry>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the entries of a kind across every account.
    /// </summary>
    Task<IReadOnlyList<QuantityLedgerEntry>> ListByKindAsync(string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entries by their ids. An id with no entry is left out.
    /// </summary>
    /// <param name="itemIds">The ids of the entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<QuantityLedgerEntry>> GetAsync(IReadOnlyCollection<string> itemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the properties of a posted entry, such as the pay runs that took a payout. The quantity, dates, kind and
    /// key stay as they were posted.
    /// </summary>
    /// <param name="entry">The entry, with its id and the properties to keep.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="false"/> when no entry has the id.</returns>
    Task<bool> SavePropertiesAsync(QuantityLedgerEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages the entries of an account, newest first, optionally of one balance.
    /// </summary>
    Task<(int Count, IReadOnlyList<QuantityLedgerEntry> Items)> PageAsync(string accountId, string bucket, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums the entries of a balance that count on a day: started on or before it and not expired.
    /// </summary>
    Task<decimal> GetBalanceAsync(string accountId, string bucket, DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums the entries of a balance effective within a period, inclusive.
    /// </summary>
    /// <summary>
    /// Lists the accounts with an entry in a bucket posted since a time, such as the people who earned credits of a
    /// type since a check last ran.
    /// </summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="postedSinceUtc">The time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account ids.</returns>
    Task<IReadOnlyCollection<string>> ListAccountsSinceAsync(string bucket, DateTime postedSinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every entry of an account, when the person it belongs to is erased.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many entries were deleted.</returns>
    Task<int> DeleteAccountAsync(string accountId, CancellationToken cancellationToken = default);

    Task<decimal> SumAsync(string accountId, string bucket, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of posting a ledger entry.
/// </summary>
/// <param name="Entry">The entry kept.</param>
/// <param name="Created">Whether it was posted now, rather than found by its key.</param>
public sealed record QuantityLedgerPostResult(QuantityLedgerEntry Entry, bool Created);
