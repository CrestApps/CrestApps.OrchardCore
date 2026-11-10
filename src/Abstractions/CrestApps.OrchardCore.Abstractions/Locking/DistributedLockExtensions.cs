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
    /// <returns>The locker, and whether the lock was acquired.</returns>
    public static Task<(ILocker locker, bool locked)> TryAcquireLockWithoutWaitingAsync(
        this IDistributedLock distributedLock,
        string key,
        TimeSpan? expiration = null)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);

        return distributedLock.TryAcquireLockAsync(key, NoWaitTimeout, expiration);
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
    /// <returns>A locker that holds every lock, or <see langword="null"/> when one couldn't be acquired.</returns>
    public static async Task<ILocker> LockAllAsync(
        this IDistributedLock distributedLock,
        IEnumerable<string> keys,
        TimeSpan timeout,
        TimeSpan? expiration = null)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(keys);

        var lockers = new List<ILocker>();

        try
        {
            foreach (var key in keys.Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var (locker, locked) = await distributedLock.TryAcquireLockAsync(key, timeout, expiration);

                if (!locked)
                {
                    await MultiKeyLocker.ReleaseAsync(lockers);

                    return null;
                }

                lockers.Add(locker);
            }
        }
        catch
        {
            await MultiKeyLocker.ReleaseAsync(lockers);

            throw;
        }

        return new MultiKeyLocker(lockers);
    }
}
