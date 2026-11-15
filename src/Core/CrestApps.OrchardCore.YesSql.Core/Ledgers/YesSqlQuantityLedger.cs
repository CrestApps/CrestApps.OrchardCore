using CrestApps.Core;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.YesSql.Core.Ledgers;

/// <summary>
/// A quantity ledger kept in its own YesSql collection, written with the caller's session.
/// </summary>
/// <remarks>
/// An entry posted twice with the same key in one session, or after the first one was committed, is kept once. Two
/// sessions that post the same key at the same moment must hold a lock of their own around the post and the commit,
/// as the owners already do around the change that posts it.
/// </remarks>
/// <typeparam name="TLedger">The marker type of the ledger.</typeparam>
public sealed class YesSqlQuantityLedger<TLedger> : IQuantityLedger<TLedger>
{
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly string _collection;
    private readonly Dictionary<string, QuantityLedgerEntry> _posted = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlQuantityLedger{TLedger}"/> class.
    /// </summary>
    public YesSqlQuantityLedger(ISession session, IClock clock, IOptions<QuantityLedgerOptions<TLedger>> options)
    {
        _session = session;
        _clock = clock;
        _collection = options.Value.CollectionName;

        if (string.IsNullOrEmpty(_collection))
        {
            throw new InvalidOperationException($"The quantity ledger '{typeof(TLedger).Name}' has no collection. Register it with AddQuantityLedger.");
        }
    }

    /// <inheritdoc/>
    public async Task<QuantityLedgerPostResult> PostAsync(QuantityLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.IsNullOrEmpty(entry.IdempotencyKey))
        {
            if (_posted.TryGetValue(entry.IdempotencyKey, out var pending))
            {
                return new QuantityLedgerPostResult(pending, false);
            }

            if (await FindByKeyAsync(entry.IdempotencyKey, cancellationToken) is { } existing)
            {
                return new QuantityLedgerPostResult(existing, false);
            }
        }

        if (string.IsNullOrEmpty(entry.ItemId))
        {
            entry.ItemId = UniqueId.GenerateId();
        }

        if (entry.CreatedUtc == default)
        {
            entry.CreatedUtc = _clock.UtcNow;
        }

        await _session.SaveAsync(entry, checkConcurrency: false, collection: _collection, cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(entry.IdempotencyKey))
        {
            _posted[entry.IdempotencyKey] = entry;
        }

        return new QuantityLedgerPostResult(entry, true);
    }

    /// <inheritdoc/>
    public Task<QuantityLedgerEntry> FindByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return Task.FromResult<QuantityLedgerEntry>(null);
        }

        return _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.IdempotencyKey == idempotencyKey, collection: _collection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QuantityLedgerEntry>> ListAsync(string accountId, string bucket, CancellationToken cancellationToken = default)
    {
        return (await _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.AccountId == accountId && index.Bucket == bucket, collection: _collection)
            .OrderBy(index => index.EffectiveDate)
            .ThenBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QuantityLedgerEntry>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return (await _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.AccountId == accountId, collection: _collection)
            .OrderBy(index => index.EffectiveDate)
            .ThenBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QuantityLedgerEntry>> ListByKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        return (await _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.Kind == kind, collection: _collection)
            .ListAsync(cancellationToken)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<QuantityLedgerEntry> Items)> PageAsync(string accountId, string bucket, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(bucket)
            ? _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.AccountId == accountId, collection: _collection)
            : _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.AccountId == accountId && index.Bucket == bucket, collection: _collection);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.EffectiveDate).ThenByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    /// <inheritdoc/>
    public async Task<decimal> GetBalanceAsync(string accountId, string bucket, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var entries = await ListAsync(accountId, bucket, cancellationToken);

        return entries.Where(entry => entry.CountsOn(asOf)).Sum(entry => entry.Amount);
    }

    /// <inheritdoc/>
    public async Task<decimal> SumAsync(string accountId, string bucket, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromKey = QuantityLedgerDates.ToKey(from);
        var toKey = QuantityLedgerDates.ToKey(to);
        var entries = await _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(
            index => index.AccountId == accountId && index.Bucket == bucket && index.EffectiveDate >= fromKey && index.EffectiveDate <= toKey,
            collection: _collection)
            .ListAsync(cancellationToken);

        return entries.Sum(entry => entry.Amount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> ListAccountsSinceAsync(string bucket, DateTime postedSinceUtc, CancellationToken cancellationToken = default)
        => (await _session.QueryIndex<QuantityLedgerEntryIndex>(index => index.Bucket == bucket && index.CreatedUtc >= postedSinceUtc, collection: _collection).ListAsync(cancellationToken))
            .Select(index => index.AccountId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <inheritdoc />
    public async Task<int> DeleteAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var entries = await _session.Query<QuantityLedgerEntry, QuantityLedgerEntryIndex>(index => index.AccountId == accountId, collection: _collection).ListAsync(cancellationToken);
        var count = 0;

        foreach (var entry in entries)
        {
            _session.Delete(entry, _collection);
            count++;
        }

        return count;
    }
}
