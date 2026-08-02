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
    private readonly ConcurrentQueue<string> _acquiredKeys = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource> _attemptSignals = new();
    private int _attemptCount;

    /// <summary>
    /// Gets every key an acquisition was actually attempted for, in acquisition order. A caller that takes a
    /// lock it already holds appears twice here, which is what makes a redundant acquisition observable.
    /// </summary>
    public IReadOnlyCollection<string> AcquiredKeys => _acquiredKeys;

    /// <summary>
    /// Returns a task that completes once the given number of acquisition attempts have reached the lock. A
    /// test awaits this before releasing whatever the first caller holds, proving a later caller is actually
    /// contending for the lock rather than taking a fast path after the first caller already released it.
    /// </summary>
    /// <param name="attemptNumber">The 1-based acquisition attempt to wait for.</param>
    /// <returns>A task that completes when that attempt has entered an acquire method.</returns>
    public Task WaitForAttemptAsync(int attemptNumber)
    {
        var signal = _attemptSignals.GetOrAdd(
            attemptNumber,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        // Complete immediately when the attempt has already happened, so a caller that registers after the
        // attempt does not wait forever.
        if (Volatile.Read(ref _attemptCount) >= attemptNumber)
        {
            signal.TrySetResult();
        }

        return signal.Task;
    }

    private void RecordAttempt(string key)
    {
        _acquiredKeys.Enqueue(key);

        var count = Interlocked.Increment(ref _attemptCount);

        if (_attemptSignals.TryGetValue(count, out var signal))
        {
            signal.TrySetResult();
        }
    }

    public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        RecordAttempt(key);

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        return new FakeLocker(() => semaphore.Release());
    }

    public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? expiration = null)
    {
        RecordAttempt(key);

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
