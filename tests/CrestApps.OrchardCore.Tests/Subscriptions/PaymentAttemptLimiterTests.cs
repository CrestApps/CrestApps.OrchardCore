using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class PaymentAttemptLimiterTests
{
    [Fact]
    public async Task TryAcquireAsync_AllowsUpToLimitThenThrottles()
    {
        var limiter = CreateLimiter(permitLimit: 3, window: TimeSpan.FromMinutes(1), out _);

        Assert.True(await limiter.TryAcquireAsync("setup-intent", "1.2.3.4:session"));
        Assert.True(await limiter.TryAcquireAsync("setup-intent", "1.2.3.4:session"));
        Assert.True(await limiter.TryAcquireAsync("setup-intent", "1.2.3.4:session"));

        // Fourth attempt within the window is throttled.
        Assert.False(await limiter.TryAcquireAsync("setup-intent", "1.2.3.4:session"));
    }

    [Fact]
    public async Task TryAcquireAsync_ResetsAfterWindowElapses()
    {
        var now = DateTime.UtcNow;
        var limiter = CreateLimiter(permitLimit: 1, window: TimeSpan.FromMinutes(1), out var clock);
        clock.Setup(c => c.UtcNow).Returns(() => now);

        Assert.True(await limiter.TryAcquireAsync("payment-intent", "ip:s"));
        Assert.False(await limiter.TryAcquireAsync("payment-intent", "ip:s"));

        // Advance beyond the window; a fresh window begins.
        now = now.AddMinutes(2);

        Assert.True(await limiter.TryAcquireAsync("payment-intent", "ip:s"));
    }

    [Fact]
    public async Task TryAcquireAsync_CountsDiscriminatorsIndependently()
    {
        var limiter = CreateLimiter(permitLimit: 1, window: TimeSpan.FromMinutes(1), out _);

        Assert.True(await limiter.TryAcquireAsync("subscription", "ipA:s"));
        Assert.False(await limiter.TryAcquireAsync("subscription", "ipA:s"));

        // A different caller/session has its own budget.
        Assert.True(await limiter.TryAcquireAsync("subscription", "ipB:s"));
    }

    [Fact]
    public async Task TryAcquireAsync_CountsScopesIndependently()
    {
        var limiter = CreateLimiter(permitLimit: 1, window: TimeSpan.FromMinutes(1), out _);

        Assert.True(await limiter.TryAcquireAsync("setup-intent", "ip:s"));
        Assert.False(await limiter.TryAcquireAsync("setup-intent", "ip:s"));

        // A different endpoint scope is independent.
        Assert.True(await limiter.TryAcquireAsync("payment-intent", "ip:s"));
    }

    [Fact]
    public async Task TryAcquireAsync_NonPositiveLimit_DisablesThrottling()
    {
        var limiter = CreateLimiter(permitLimit: 0, window: TimeSpan.FromMinutes(1), out _);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(await limiter.TryAcquireAsync("setup-intent", "ip:s"));
        }
    }

    [Fact]
    public async Task TryAcquireAsync_LockNotAcquired_FailsClosed()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), false));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        var limiter = new PaymentAttemptLimiter(
            cache,
            distributedLock.Object,
            new ShellSettings { Name = "Default" },
            clock.Object,
            Options.Create(new PaymentRateLimitOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

        Assert.False(await limiter.TryAcquireAsync("setup-intent", "ip:s"));
    }

    private static PaymentAttemptLimiter CreateLimiter(int permitLimit, TimeSpan window, out Mock<IClock> clock)
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        return new PaymentAttemptLimiter(
            cache,
            distributedLock.Object,
            new ShellSettings { Name = "Default" },
            clock.Object,
            Options.Create(new PaymentRateLimitOptions { PermitLimit = permitLimit, Window = window }));
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
