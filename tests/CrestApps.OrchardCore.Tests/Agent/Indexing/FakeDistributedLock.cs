using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Agent.Indexing;

/// <summary>
/// Stands in for the distributed lock in a tool test: it grants or refuses every lock as the test sets it, and records
/// the keys that were asked for and how many locks were released.
/// </summary>
internal sealed class FakeDistributedLock : IDistributedLock
{
    /// <summary>
    /// Gets or sets a value indicating whether a lock can be taken.
    /// </summary>
    public bool Available { get; set; } = true;

    /// <summary>
    /// Gets the keys of the locks that were asked for.
    /// </summary>
    public List<string> Keys { get; } = [];

    /// <summary>
    /// Gets the number of locks that were released.
    /// </summary>
    public int Released { get; private set; }

    public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
    {
        Keys.Add(key);

        return Task.FromResult<ILocker>(new Locker(this));
    }

    public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
    {
        Keys.Add(key);

        return Task.FromResult<(ILocker, bool)>(Available
            ? (new Locker(this), true)
            : (null, false));
    }

    public Task<bool> IsLockAcquiredAsync(string key)
        => Task.FromResult(false);

    private sealed class Locker : ILocker
    {
        private readonly FakeDistributedLock _owner;

        public Locker(FakeDistributedLock owner)
        {
            _owner = owner;
        }

        public void Dispose()
            => _owner.Released++;

        public ValueTask DisposeAsync()
        {
            _owner.Released++;

            return ValueTask.CompletedTask;
        }
    }
}
