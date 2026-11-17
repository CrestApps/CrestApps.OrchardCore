using System.Collections.Concurrent;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A distributed lock held in memory, one waiting semaphore per key: a second holder waits up to the timeout, as the
/// real lock does, so concurrency tests see one holder at a time.
/// </summary>
internal sealed class KeyedDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets how many times a lock was taken, by key.
    /// </summary>
    public ConcurrentDictionary<string, int> Taken { get; } = new(StringComparer.Ordinal);

    public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        Taken.AddOrUpdate(key, 1, (_, count) => count + 1);

        return new Locker(semaphore);
    }

    public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        if (!await semaphore.WaitAsync(timeout))
        {
            return (null, false);
        }

        Taken.AddOrUpdate(key, 1, (_, count) => count + 1);

        return (new Locker(semaphore), true);
    }

    public Task<bool> IsLockAcquiredAsync(string key)
        => Task.FromResult(_locks.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);

    private sealed class Locker : ILocker
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Locker(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
