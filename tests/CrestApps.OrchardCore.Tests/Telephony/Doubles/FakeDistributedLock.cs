using System.Collections.Concurrent;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A deterministic in-memory <see cref="IDistributedLock"/> that serializes callers per key using a
/// real semaphore, so concurrency tests can prove that locked critical sections cannot interleave.
/// </summary>
internal sealed class FakeDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        return new FakeLocker(() => semaphore.Release());
    }

    public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? expiration = null)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var locked = await semaphore.WaitAsync(timeout);

        return locked
            ? (new FakeLocker(() => semaphore.Release()), true)
            : (null, false);
    }

    public Task<bool> IsLockAcquiredAsync(string key)
    {
        return Task.FromResult(
            _locks.TryGetValue(key, out var semaphore) &&
            semaphore.CurrentCount == 0);
    }

    private sealed class FakeLocker : ILocker
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public FakeLocker(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            DisposeCore();
        }

        public ValueTask DisposeAsync()
        {
            DisposeCore();

            return ValueTask.CompletedTask;
        }

        private void DisposeCore()
        {
            if (_disposed)
            {
                return;
            }

            _onDispose();
            _disposed = true;
        }
    }
}
