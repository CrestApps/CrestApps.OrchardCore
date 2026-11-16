using System.Collections.Concurrent;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Learning;

// Distributed lock doubles for the Learning write tests: one that records the keys it is asked for, and one that
// really serializes callers per key, so concurrent scopes race as they would on a site.
internal sealed class RecordingLock : IDistributedLock
{
    public ConcurrentBag<string> Keys { get; } = [];

    public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        Keys.Add(key);

        return Task.FromResult<ILocker>(new Releaser(null));
    }

    public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        Keys.Add(key);

        return Task.FromResult<(ILocker, bool)>((new Releaser(null), true));
    }

    public Task<bool> IsLockAcquiredAsync(string key)
        => Task.FromResult(false);
}

internal sealed class KeyedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

    public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        return new Releaser(semaphore);
    }

    public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        return await semaphore.WaitAsync(timeout) ? (new Releaser(semaphore), true) : (null, false);
    }

    public Task<bool> IsLockAcquiredAsync(string key)
        => Task.FromResult(_semaphores.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);
}

// A lock that refuses the keys starting with a prefix the next number of times a test asks for (or always), as a lock
// held by another request would, and grants every other key at once.
internal sealed class BusyLock : IDistributedLock
{
    private readonly string _prefix;
    private int _refusalsLeft;

    public BusyLock(string prefix, int refusals = 0)
    {
        _prefix = prefix;
        _refusalsLeft = refusals;
    }

    public int Refused { get; private set; }

    public void RefuseNext(int refusals)
    {
        _refusalsLeft = refusals;
        Refused = 0;
    }

    public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        => Task.FromResult<ILocker>(new Releaser(null));

    public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        if (key.StartsWith(_prefix, StringComparison.Ordinal) && Interlocked.Decrement(ref _refusalsLeft) >= 0)
        {
            Refused++;

            return Task.FromResult<(ILocker, bool)>((null, false));
        }

        return Task.FromResult<(ILocker, bool)>((new Releaser(null), true));
    }

    public Task<bool> IsLockAcquiredAsync(string key)
        => Task.FromResult(false);
}

internal sealed class Releaser : ILocker
{
    private readonly SemaphoreSlim _semaphore;

    public Releaser(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public ValueTask DisposeAsync()
    {
        _semaphore?.Release();

        return ValueTask.CompletedTask;
    }

    public void Dispose()
        => _semaphore?.Release();
}
