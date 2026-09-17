using CrestApps.Core.Locking;
using OrchardCore.Locking.Distributed;
using OrchardLocker = OrchardCore.Locking.ILocker;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Binds the framework's <see cref="IDistributedLockProvider"/> to Orchard Core's distributed lock,
/// so a tenant keeps whichever lock implementation it has configured.
/// </summary>
public sealed class OrchardCoreDistributedLockProvider : IDistributedLockProvider
{
    private readonly IDistributedLock _distributedLock;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreDistributedLockProvider"/> class.
    /// </summary>
    /// <param name="distributedLock">The Orchard Core distributed lock.</param>
    public OrchardCoreDistributedLockProvider(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    /// <inheritdoc/>
    public async Task<(ILocker Locker, bool Locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(key, timeout, expiration);

        return (new LockerAdapter(locker), locked);
    }

    /// <inheritdoc/>
    public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        => new LockerAdapter(await _distributedLock.AcquireLockAsync(key, expiration));

    /// <inheritdoc/>
    public Task<bool> IsLockAcquiredAsync(string key, CancellationToken cancellationToken = default)
        => _distributedLock.IsLockAcquiredAsync(key);

    private sealed class LockerAdapter : ILocker
    {
        private readonly OrchardLocker _locker;

        public LockerAdapter(OrchardLocker locker)
        {
            _locker = locker;
        }

        public void Dispose() => _locker.Dispose();

        public ValueTask DisposeAsync() => _locker.DisposeAsync();
    }
}
