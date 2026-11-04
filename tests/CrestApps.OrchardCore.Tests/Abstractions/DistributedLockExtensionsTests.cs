using CrestApps.OrchardCore.Locking;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Abstractions;

public sealed class DistributedLockExtensionsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task LockAllAsync_TakesEachKeyOnceInOrdinalOrder_AndSkipsEmptyKeys()
    {
        // Arrange
        var distributedLock = new RecordingLock();

        // Act
        var locker = await distributedLock.LockAllAsync(["b", "a", null, "B", "", "a", "c"], _timeout);

        // Assert
        Assert.NotNull(locker);
        Assert.Equal(["B", "a", "b", "c"], distributedLock.Acquired);
        Assert.Empty(distributedLock.Released);

        await locker.DisposeAsync();

        Assert.Equal(["c", "b", "a", "B"], distributedLock.Released);
    }

    [Fact]
    public async Task LockAllAsync_WhenOneKeyIsHeld_ReleasesTheKeysAlreadyTaken_AndReturnsNull()
    {
        // Arrange
        var distributedLock = new RecordingLock { Held = { "c" } };

        // Act
        var locker = await distributedLock.LockAllAsync(["d", "c", "b", "a"], _timeout);

        // Assert
        Assert.Null(locker);
        Assert.Equal(["a", "b"], distributedLock.Acquired);
        Assert.Equal(["b", "a"], distributedLock.Released);
    }

    [Fact]
    public async Task LockAllAsync_WhenTakingAKeyThrows_ReleasesTheKeysAlreadyTaken()
    {
        // Arrange
        var distributedLock = new RecordingLock { Failing = { "b" } };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => distributedLock.LockAllAsync(["a", "b", "c"], _timeout));

        // Assert
        Assert.Equal(["a"], distributedLock.Acquired);
        Assert.Equal(["a"], distributedLock.Released);
    }

    [Fact]
    public async Task LockAllAsync_WithNoKeys_ReturnsALockerThatHoldsNothing()
    {
        // Arrange
        var distributedLock = new RecordingLock();

        // Act
        await using var locker = await distributedLock.LockAllAsync([], _timeout);

        // Assert
        Assert.NotNull(locker);
        Assert.Empty(distributedLock.Acquired);
    }

    [Fact]
    public async Task LockAllAsync_TwoCallersOverTheSameKeysInAnyOrder_NeverHoldThemTogether()
    {
        // Arrange
        var distributedLock = new LocalLock(Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalLock>.Instance);
        var first = await distributedLock.LockAllAsync(["x", "y"], _timeout);

        // Act
        var second = await distributedLock.LockAllAsync(["y", "x"], TimeSpan.FromMilliseconds(100));

        await first.DisposeAsync();

        var third = await distributedLock.LockAllAsync(["y", "x"], _timeout);

        // Assert
        Assert.Null(second);
        Assert.NotNull(third);

        await third.DisposeAsync();
    }

    private sealed class RecordingLock : IDistributedLock
    {
        public List<string> Acquired { get; } = [];

        public List<string> Released { get; } = [];

        public HashSet<string> Held { get; } = [];

        public HashSet<string> Failing { get; } = [];

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => throw new NotSupportedException();

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            if (Failing.Contains(key))
            {
                throw new InvalidOperationException("The lock store is down.");
            }

            if (Held.Contains(key))
            {
                return Task.FromResult<(ILocker, bool)>((null, false));
            }

            Acquired.Add(key);

            return Task.FromResult<(ILocker, bool)>((new RecordingLocker(this, key), true));
        }

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(Acquired.Contains(key) && !Released.Contains(key));
    }

    private sealed class RecordingLocker : ILocker
    {
        private readonly RecordingLock _owner;
        private readonly string _key;

        public RecordingLocker(RecordingLock owner, string key)
        {
            _owner = owner;
            _key = key;
        }

        public ValueTask DisposeAsync()
        {
            _owner.Released.Add(_key);

            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            _owner.Released.Add(_key);
        }
    }
}
