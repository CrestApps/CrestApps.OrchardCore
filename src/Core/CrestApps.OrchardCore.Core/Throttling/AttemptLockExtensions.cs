using System.Security.Cryptography;
using System.Text;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Core.Throttling;

/// <summary>
/// Serializes the tries of one key against a policy that counts failures, such as wrong codes. A caller that peeks,
/// does the work and then records a failure lets parallel tries all pass the peek before any failure is counted; holding
/// this lock across the three steps makes each try see the failures before it.
/// </summary>
public static class AttemptLockExtensions
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _expiration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Takes the lock for the tries of <paramref name="key"/> against <paramref name="policyName"/>. When it can't be
    /// taken, refuse the try: that coincides with a burst from the key.
    /// </summary>
    /// <param name="distributedLock">The tenant's lock.</param>
    /// <param name="policyName">The attempt policy.</param>
    /// <param name="key">What is counted, such as a network. It is hashed, so no address is part of the lock name.</param>
    public static Task<(ILocker Locker, bool Locked)> TryAcquireAttemptLockAsync(this IDistributedLock distributedLock, string policyName, string key)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentException.ThrowIfNullOrEmpty(policyName);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 16);

        return distributedLock.TryAcquireLockAsync($"AttemptTries:{policyName}:{hash}", _timeout, _expiration);
    }
}
