using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Covers the per-user count behind the Messaging admin menu badge, which every admin page load asks for.
/// </summary>
public sealed class MessagingAttentionCountCacheTests
{
    private static readonly DateTime _start = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetOrReadAsync_WithinTheLifetime_DoesNotReadAgain()
    {
        var (cache, _) = CreateCache();
        var reads = 0;

        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);
        var count = await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task GetOrReadAsync_OnceTheLifetimeHasPassed_ReadsAgain()
    {
        var (cache, clock) = CreateCache();
        var reads = 0;

        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);
        clock.SetupGet(value => value.UtcNow).Returns(_start + MessagingAttentionCountCache.Lifetime);
        var count = await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetOrReadAsync_WithRefresh_ReadsAgainAndKeepsTheNewCount()
    {
        var (cache, _) = CreateCache();
        var reads = 0;

        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);
        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: true, TestContext.Current.CancellationToken);
        var count = await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
        Assert.Equal(2, reads);
    }

    // The count depends on who is asking: each user's assignments, queues and permissions.
    [Fact]
    public async Task GetOrReadAsync_KeepsEachUsersCountApart()
    {
        var (cache, _) = CreateCache();

        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(4), refresh: false, TestContext.Current.CancellationToken);
        var count = await cache.GetOrReadAsync("user-2", _ => Task.FromResult(7), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(7, count);
    }

    [Fact]
    public async Task GetOrReadAsync_ForAUserWithoutAnIdentifier_NeverKeepsTheCount()
    {
        var (cache, _) = CreateCache();
        var reads = 0;

        await cache.GetOrReadAsync(null, _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);
        await cache.GetOrReadAsync(null, _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task Remove_MakesTheNextRequestRead()
    {
        var (cache, _) = CreateCache();
        var reads = 0;

        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);
        cache.Remove("user-1");
        await cache.GetOrReadAsync("user-1", _ => Task.FromResult(++reads), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(2, reads);
    }

    // A failed read is not kept, so the next page load tries again rather than showing nothing for the lifetime.
    [Fact]
    public async Task GetOrReadAsync_WhenTheReadFails_KeepsNothing()
    {
        var (cache, _) = CreateCache();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrReadAsync(
            "user-1", _ => throw new InvalidOperationException(), refresh: false, TestContext.Current.CancellationToken));
        var count = await cache.GetOrReadAsync("user-1", _ => Task.FromResult(3), refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(3, count);
    }

    private static (MessagingAttentionCountCache Cache, Mock<IClock> Clock) CreateCache()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_start);

        return (new MessagingAttentionCountCache(clock.Object), clock);
    }
}
