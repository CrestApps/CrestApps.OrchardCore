using CrestApps.OrchardCore.Tests.Doubles;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// An in-memory distributed lock that really waits for the daily invitation e-mail lock, but for a short time instead of
/// the requested timeout, and counts every attempt to take it, so a test sees how often a send waited.
/// </summary>
internal sealed class InvitationEmailLockDouble : IDistributedLock
{
    private const string EmailPrefix = "Surveys:InvitationEmails:";

    private readonly KeyedDistributedLock _inner = new();
    private int _attempts;

    public InvitationEmailLockDouble(TimeSpan wait)
    {
        Wait = wait;
    }

    /// <summary>
    /// Gets how long one attempt waits for the e-mail lock.
    /// </summary>
    public TimeSpan Wait { get; }

    /// <summary>
    /// Gets how many times a sender asked for the e-mail lock.
    /// </summary>
    public int Attempts => Volatile.Read(ref _attempts);

    public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        => _inner.AcquireLockAsync(key, expiration);

    public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        if (!key.StartsWith(EmailPrefix, StringComparison.Ordinal))
        {
            return _inner.TryAcquireLockAsync(key, timeout, expiration);
        }

        Interlocked.Increment(ref _attempts);

        return _inner.TryAcquireLockAsync(key, Wait, expiration);
    }

    public Task<bool> IsLockAcquiredAsync(string key)
        => _inner.IsLockAcquiredAsync(key);
}
