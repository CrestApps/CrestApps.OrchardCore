using CrestApps.OrchardCore.Locking;
using CrestApps.OrchardCore.Numbering;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Numbering;

/// <summary>
/// The <see cref="INumberSequence"/> on YesSql: one document per series, read and saved in the caller's unit of work.
/// </summary>
/// <remarks>
/// Generalized from the financial document number generator in Transactions. A distributed lock on the series is taken
/// before the last number is read and held until the caller's unit of work has been saved (the end of the shell scope,
/// or of the service scope outside a shell scope), so two nodes or two requests never read the same last number. The
/// caller's session is used rather than a separate one, because on SQLite a second
/// connection writing while the first holds its write transaction would wait on it forever. When the unit of work fails,
/// the number is not saved, so the next issuer gets it and the series has no gaps.
/// </remarks>
internal sealed class DocumentNumberSequence : INumberSequence, IAsyncDisposable
{
    private const string LockKeyPrefix = "CrestApps.NumberSequence:";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly Dictionary<string, ILocker> _lockers = new(StringComparer.Ordinal);

    public DocumentNumberSequence(ISession session, IDistributedLock distributedLock)
    {
        _session = session;
        _distributedLock = distributedLock;
    }

    public async Task<long> NextAsync(string series, NumberSequenceOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(series);

        await HoldLockAsync(series);

        var records = await _session.Query<NumberSequenceRecord>().ListAsync(cancellationToken);
        var record = records.FirstOrDefault(candidate => string.Equals(candidate.Series, series, StringComparison.Ordinal));

        if (record is null)
        {
            var firstNumber = options?.FirstNumber ?? 1;
            var highestInUse = options?.SeedAsync is null ? 0 : await options.SeedAsync(cancellationToken);

            record = new NumberSequenceRecord
            {
                Series = series,
                Last = Math.Max(firstNumber - 1, highestInUse),
            };
        }

        record.Last++;

        await _session.SaveAsync(record, checkConcurrency: true, cancellationToken: cancellationToken);

        return record.Last;
    }

    private async Task HoldLockAsync(string series)
    {
        if (_lockers.ContainsKey(series))
        {
            return;
        }

        var (locker, locked) = await _distributedLock.TryAcquireMeasuredLockAsync(LockKeyPrefix + series, _lockTimeout, _lockExpiration, "yessql.number_sequence");

        if (!locked)
        {
            throw new TimeoutException($"Another process is issuing a number in the '{series}' series. Try again in a moment.");
        }

        _lockers[series] = locker;

        // Released once this unit of work has been saved, so the next issuer reads the number saved here: after the shell
        // scope, or, outside one, when the service scope that owns the session ends. If the work fails, the lock simply
        // expires.
        if (ShellScope.Current is not null)
        {
            ShellScope.AddDeferredTask(_ => ReleaseLockAsync(series));
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var series in _lockers.Keys.ToArray())
        {
            await ReleaseLockAsync(series);
        }
    }

    private async Task ReleaseLockAsync(string series)
    {
        if (_lockers.Remove(series, out var locker))
        {
            await locker.DisposeAsync();
        }
    }
}
