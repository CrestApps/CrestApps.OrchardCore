using CrestApps.Core.Hosting.Locking;
using Microsoft.Extensions.Time.Testing;

namespace CrestApps.OrchardCore.Tests.Framework.Hosting;

/// <summary>
/// Pins the framework's default lock.
/// </summary>
/// <remarks>
/// No Orchard Core startup registers this — Orchard binds its own tenant lock instead — so nothing
/// else in this repository exercises it. It is the only implementation a standalone host gets, and
/// the suite uses locks to stop two nodes dialling the same contact twice, so the failure it would
/// hide is a duplicate call rather than a broken page.
/// </remarks>
public sealed class LocalDistributedLockProviderTests
{
    [Fact]
    public async Task TryAcquireLockAsync_WhenTheKeyIsFree_Grants()
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());

        // Act
        var (locker, locked) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(locked);
        Assert.NotNull(locker);

        await locker.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireLockAsync_WhileHeld_DoesNotGrantASecondTime()
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());
        var (first, _) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var (_, locked) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: this is the whole point of the type. Two holders means two dialers.
        Assert.False(locked);

        await first.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ForADifferentKey_IsUnaffected()
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());
        var (first, _) = await provider.TryAcquireLockAsync("one", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var (second, locked) = await provider.TryAcquireLockAsync("two", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(locked);

        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task ReleasingALock_LetsTheNextCallerIn()
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());
        var (first, _) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await first.DisposeAsync();
        var (second, locked) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(locked);

        await second.DisposeAsync();
    }

    [Fact]
    public async Task DisposingTwice_DoesNotHandTheLockToTwoCallers()
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());
        var (first, _) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Act: a double release would raise the semaphore count above one, and then two callers
        // could hold the same key at once.
        await first.DisposeAsync();
        await first.DisposeAsync();

        var (second, _) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        var (_, thirdLocked) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(thirdLocked);

        await second.DisposeAsync();
    }

    [Fact]
    public async Task AnExpiredLock_IsReleasedWithoutTheHolderDoingAnything()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var provider = new LocalDistributedLockProvider(timeProvider);
        var (locker, _) = await provider.TryAcquireLockAsync(
            "key",
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);

        // Act: a holder that dies mid-work must not keep the key forever.
        timeProvider.Advance(TimeSpan.FromSeconds(31));

        var (second, locked) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(locked);

        await locker.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task IsLockAcquiredAsync_ReportsWhetherTheKeyIsHeld()
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());

        // Act & Assert
        Assert.False(await provider.IsLockAcquiredAsync("key", TestContext.Current.CancellationToken));

        var (locker, _) = await provider.TryAcquireLockAsync("key", TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await provider.IsLockAcquiredAsync("key", TestContext.Current.CancellationToken));

        await locker.DisposeAsync();

        Assert.False(await provider.IsLockAcquiredAsync("key", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AnEmptyKey_IsRejected(string key)
    {
        // Arrange
        var provider = new LocalDistributedLockProvider(new FakeTimeProvider());

        // Act & Assert: an empty key would put unrelated work behind one lock.
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.TryAcquireLockAsync(key, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.AcquireLockAsync(key, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => provider.IsLockAcquiredAsync(key, TestContext.Current.CancellationToken));
    }
}
