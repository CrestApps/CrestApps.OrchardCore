using System.Diagnostics;
using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Copies the time-off entries of the Workforce collection, where installs before the shared quantity ledger kept
/// them, to the time-off ledger collection. The copy is additive: the original entries stay, so a node of the earlier
/// release keeps reading them during a rolling upgrade. It runs at every start of the tenant until a later release
/// drops the old entries, so entries such a node writes after an earlier copy are picked up too; an entry is skipped
/// when its id or its idempotency key is already in the ledger, so nothing lands twice. The first copy that reads to
/// the end records that it is complete, and accrual waits for that record, so it never re-posts accruals into a ledger
/// that is still missing its history.
/// </summary>
public sealed class TimeOffLedgerCopy
{
    /// <summary>
    /// The number of old entries copied and committed at a time.
    /// </summary>
    public const int DefaultBatchSize = 500;

    /// <summary>
    /// The lock one copy holds at a time across the nodes of a farm.
    /// </summary>
    public const string LockKey = "Workforce:TimeOffLedgerCopy";

    // Sized for a large ledger: a copy of hundreds of thousands of entries in batches of 500 finishes well inside it.
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromHours(2);

    // A copy stops taking new batches this long after it took the lock, well before the lock expires, so a second
    // node can never copy beside it; the next start goes on from where it stopped.
    private static readonly TimeSpan _maxRunTime = TimeSpan.FromMinutes(90);

    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffLedgerCopy"/> class.
    /// </summary>
    public TimeOffLedgerCopy(ISession session, IDistributedLock distributedLock, IClock clock, ILogger<TimeOffLedgerCopy> logger)
    {
        _session = session;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets a step run after each committed batch. Tests use it to stop a copy half way.
    /// </summary>
    internal Func<int, Task> AfterBatch { get; set; }

    /// <summary>
    /// Copies every entry not copied yet, keeping its id, a batch at a time, and records the copy complete once it read
    /// to the end.
    /// </summary>
    /// <param name="batchSize">The number of entries committed at a time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many entries were copied.</returns>
    public async Task<int> CopyAsync(int batchSize = DefaultBatchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        // Nodes of a farm start together after a deployment. Without the lock each would read the ledger before the
        // other wrote and both would copy every entry, doubling every balance for good. The node that does not get the
        // lock skips the run: the holder, or the next start, does the work.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey, TimeSpan.Zero, _lockExpiration);

        if (!locked)
        {
            _logger.LogInformation("Another node is copying the time-off ledger; this start skips the copy.");

            return 0;
        }

        // Released in the finally of the using block, whatever the copy does.
        await using (locker)
        {
            return await CopyLockedAsync(batchSize, Stopwatch.StartNew(), cancellationToken);
        }
    }

    /// <summary>
    /// Gets or sets how long a copy may run under its lock before it stops taking batches. Tests make it short.
    /// </summary>
    internal TimeSpan MaxRunTime { get; set; } = _maxRunTime;

    private async Task<int> CopyLockedAsync(int batchSize, Stopwatch elapsed, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in await _session.QueryIndex<QuantityLedgerEntryIndex>(collection: WorkforceConstants.TimeOffLedgerCollectionName).ListAsync(cancellationToken))
        {
            ids.Add(row.ItemId);

            if (!string.IsNullOrEmpty(row.IdempotencyKey))
            {
                keys.Add(row.IdempotencyKey);
            }
        }

        var count = 0;

        for (var skip = 0; ; skip += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = (await _session.Query<TimeOffLedgerEntry>(collection: WorkforceConstants.CollectionName)
                .Skip(skip)
                .Take(batchSize)
                .ListAsync(cancellationToken)).ToArray();

            foreach (var entry in batch)
            {
                // A copy that was cut short, or an accrual posted again while the ledger was empty, may already hold an
                // entry with this key under another id.
                if (ids.Contains(entry.ItemId) || (!string.IsNullOrEmpty(entry.IdempotencyKey) && keys.Contains(entry.IdempotencyKey)))
                {
                    continue;
                }

                QuantityLedgerEntry quantity = TimeOffLedgerMapping.ToQuantity(entry);

                await _session.SaveAsync(quantity, checkConcurrency: false, collection: WorkforceConstants.TimeOffLedgerCollectionName, cancellationToken: cancellationToken);
                ids.Add(entry.ItemId);

                if (!string.IsNullOrEmpty(entry.IdempotencyKey))
                {
                    keys.Add(entry.IdempotencyKey);
                }

                count++;
            }

            await _session.SaveChangesAsync(cancellationToken);

            if (AfterBatch is not null)
            {
                await AfterBatch(skip + batch.Length);
            }

            if (batch.Length < batchSize)
            {
                break;
            }

            if (elapsed.Elapsed >= MaxRunTime)
            {
                // Out of time before the lock could expire: what was copied is committed, the copy is not recorded
                // complete, and the next start goes on.
                _logger.LogWarning("The time-off ledger copy stopped before its lock expires; the next start goes on.");

                return count;
            }
        }

        var state = await LoadStateAsync(cancellationToken);

        if (state.CompletedUtc is null)
        {
            state.CompletedUtc = _clock.UtcNow;
            state.Copied = count;
            await _session.SaveAsync(state, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("The time-off ledger copy is complete: {Count} entries were copied.", count);
            }
        }

        return count;
    }

    /// <summary>
    /// Tells whether the ledger holds its history: the copy recorded that it read to the end, or there is nothing to
    /// copy (an install that started on the shared ledger).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when accrual may post.</returns>
    public async Task<bool> IsCompleteAsync(CancellationToken cancellationToken = default)
    {
        if ((await LoadStateAsync(cancellationToken)).CompletedUtc is not null)
        {
            return true;
        }

        return await _session.Query<TimeOffLedgerEntry>(collection: WorkforceConstants.CollectionName).CountAsync(cancellationToken) == 0;
    }

    private async Task<TimeOffLedgerCopyState> LoadStateAsync(CancellationToken cancellationToken)
        => await _session.Query<TimeOffLedgerCopyState>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new TimeOffLedgerCopyState();
}

/// <summary>
/// Records that the time-off ledger copy read to the end once.
/// </summary>
public sealed class TimeOffLedgerCopyState
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets when the first complete copy finished, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many entries that copy wrote.
    /// </summary>
    public int Copied { get; set; }
}
