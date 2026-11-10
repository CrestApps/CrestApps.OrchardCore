using System.Diagnostics;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Locking;

/// <summary>
/// Extensions for <see cref="IDistributedLock"/>.
/// </summary>
public static class DistributedLockExtensions
{
    /// <summary>
    /// The window a no-wait acquisition passes as its timeout.
    /// </summary>
    /// <remarks>
    /// Orchard Core's Redis lock starts its retry loop on a cancellation source built from the timeout, and one built from
    /// <see cref="TimeSpan.Zero"/> is already canceled, so a zero timeout returns "not locked" without ever asking Redis.
    /// A short positive window makes one real attempt on every implementation; the in-process lock waits at most this long
    /// for a holder to let go.
    /// </remarks>
    public static readonly TimeSpan NoWaitTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Tries once to acquire a named lock, without waiting for a holder to release it.
    /// </summary>
    /// <remarks>
    /// Use this instead of passing <see cref="TimeSpan.Zero"/> as the timeout: with the Redis lock enabled a zero timeout
    /// never acquires the lock, even when nobody holds it.
    /// </remarks>
    /// <param name="distributedLock">The lock service.</param>
    /// <param name="key">The name of the lock.</param>
    /// <param name="expiration">When the lock is released on its own, or <see langword="null"/> for never.</param>
    /// <param name="area">The area that takes the lock, from a bounded set such as <c>contactcenter.reservation</c>,
    /// recorded on <see cref="LockTelemetry"/>. Never the key, which holds record ids.</param>
    /// <returns>The locker, and whether the lock was acquired.</returns>
    public static Task<(ILocker locker, bool locked)> TryAcquireLockWithoutWaitingAsync(
        this IDistributedLock distributedLock,
        string key,
        TimeSpan? expiration = null,
        string area = null)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);

        return AcquireAndRecordAsync(distributedLock, key, NoWaitTimeout, expiration, area, LockTelemetry.KindNoWait);
    }

    /// <summary>
    /// Tries to acquire a named lock, waiting up to <paramref name="timeout"/> for a holder to release it, and records
    /// the attempt on <see cref="LockTelemetry"/>.
    /// </summary>
    /// <param name="distributedLock">The lock service.</param>
    /// <param name="key">The name of the lock.</param>
    /// <param name="timeout">How long to wait for the lock.</param>
    /// <param name="expiration">When the lock is released on its own, or <see langword="null"/> for never.</param>
    /// <param name="area">The area that takes the lock, from a bounded set such as <c>contactcenter.reservation</c>.
    /// Never the key, which holds record ids.</param>
    /// <returns>The locker, and whether the lock was acquired.</returns>
    public static Task<(ILocker locker, bool locked)> TryAcquireMeasuredLockAsync(
        this IDistributedLock distributedLock,
        string key,
        TimeSpan timeout,
        TimeSpan? expiration,
        string area)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);

        return AcquireAndRecordAsync(distributedLock, key, timeout, expiration, area, LockTelemetry.KindWait);
    }

    /// <summary>
    /// Acquires several named locks, all or nothing.
    /// </summary>
    /// <remarks>
    /// The keys are taken once each, in ordinal order, so two callers that lock overlapping keys never wait on each other
    /// in a circle. Empty keys are skipped. When a key can't be taken in time, or taking it throws, the keys already taken
    /// are released before the method returns or throws. Disposing the returned locker releases every key, in reverse
    /// order.
    /// </remarks>
    /// <param name="distributedLock">The lock service.</param>
    /// <param name="keys">The names of the locks.</param>
    /// <param name="timeout">How long to wait for each lock.</param>
    /// <param name="expiration">When each lock is released on its own, or <see langword="null"/> for never.</param>
    /// <param name="area">The area that takes the locks, from a bounded set, recorded on <see cref="LockTelemetry"/>.</param>
    /// <returns>A locker that holds every lock, or <see langword="null"/> when one couldn't be acquired.</returns>
    public static async Task<ILocker> LockAllAsync(
        this IDistributedLock distributedLock,
        IEnumerable<string> keys,
        TimeSpan timeout,
        TimeSpan? expiration = null,
        string area = null)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(keys);

        var startTimestamp = Stopwatch.GetTimestamp();
        var lockers = new List<ILocker>();

        try
        {
            foreach (var key in keys.Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var (locker, locked) = await distributedLock.TryAcquireLockAsync(key, timeout, expiration);

                if (!locked)
                {
                    await MultiKeyLocker.ReleaseAsync(lockers);
                    LockTelemetry.Record(area, LockTelemetry.KindMulti, LockTelemetry.ResultTimeout, startTimestamp);

                    return null;
                }

                lockers.Add(locker);
            }
        }
        catch
        {
            await MultiKeyLocker.ReleaseAsync(lockers);
            LockTelemetry.Record(area, LockTelemetry.KindMulti, LockTelemetry.ResultError, startTimestamp);

            throw;
        }

        LockTelemetry.Record(area, LockTelemetry.KindMulti, LockTelemetry.ResultAcquired, startTimestamp);

        return new MultiKeyLocker(lockers);
    }

    private static async Task<(ILocker locker, bool locked)> AcquireAndRecordAsync(
        IDistributedLock distributedLock,
        string key,
        TimeSpan timeout,
        TimeSpan? expiration,
        string area,
        string kind)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var result = await distributedLock.TryAcquireLockAsync(key, timeout, expiration);

            LockTelemetry.Record(
                area,
                kind,
                result.locked
                    ? LockTelemetry.ResultAcquired
                    : kind == LockTelemetry.KindNoWait
                        ? LockTelemetry.ResultContended
                        : LockTelemetry.ResultTimeout,
                startTimestamp);

            return result;
        }
        catch
        {
            LockTelemetry.Record(area, kind, LockTelemetry.ResultError, startTimestamp);

            throw;
        }
    }
}
