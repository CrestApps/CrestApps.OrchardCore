using System.Collections.Concurrent;
using CrestApps.Core.Locking;

namespace CrestApps.Core.Hosting.Locking;

/// <summary>
/// An in-process <see cref="IDistributedLockProvider"/>. It serialises work correctly on a single
/// node and is the default so a host that has not configured real distributed locking still behaves
/// correctly; it does not coordinate across nodes, so a multi-node host must replace it.
/// </summary>
public sealed class LocalDistributedLockProvider : IDistributedLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDistributedLockProvider"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider used to expire held locks.</param>
    public LocalDistributedLockProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<(ILocker Locker, bool Locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var semaphore = _semaphores.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));

        if (!await semaphore.WaitAsync(timeout, cancellationToken))
        {
            return (NullLocker.Instance, false);
        }

        return (new Locker(semaphore, expiration, _timeProvider), true);
    }

    /// <inheritdoc/>
    public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var semaphore = _semaphores.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);

        return new Locker(semaphore, expiration, _timeProvider);
    }

    /// <inheritdoc/>
    public Task<bool> IsLockAcquiredAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return Task.FromResult(_semaphores.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);
    }

    private sealed class Locker : ILocker
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ITimer _expiryTimer;
        private int _released;

        public Locker(SemaphoreSlim semaphore, TimeSpan? expiration, TimeProvider timeProvider)
        {
            _semaphore = semaphore;

            if (expiration.HasValue)
            {
                _expiryTimer = timeProvider.CreateTimer(static state => ((Locker)state).Release(), this, expiration.Value, Timeout.InfiniteTimeSpan);
            }
        }

        public void Dispose()
        {
            Release();
            _expiryTimer?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }

        private void Release()
        {
            // The expiry timer and Dispose race, and releasing the semaphore twice would hand the
            // lock to two callers at once.
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }

    private sealed class NullLocker : ILocker
    {
        public static readonly NullLocker Instance = new();

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
