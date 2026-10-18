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
}
