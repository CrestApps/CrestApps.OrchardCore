using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

#pragma warning disable CS0618 // The obsolete limiter is kept for one release; these tests pin what it still does.

/// <summary>
/// RL-1: the old payment attempt limiter is now an adapter over the platform attempt limiter, kept for one release.
/// The limiter's own behavior is covered by <c>DistributedAttemptLimiterTests</c>.
/// </summary>
public sealed class PaymentAttemptLimiterTests
{
    [Fact]
    public async Task AcquireAsync_CountsTheScopeAndDiscriminatorAgainstTheLegacyPolicy()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter.Setup(l => l.AcquireAsync(PaymentAttemptLimiter.LegacyPolicyName, "setup-intent:1.2.3.4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(1)));

        var adapter = new PaymentAttemptLimiter(limiter.Object, Options.Create(new PaymentRateLimitOptions()));

        Assert.False(await adapter.AcquireAsync("setup-intent", "1.2.3.4"));
    }

    [Fact]
    public async Task AcquireAsync_WithANonPositiveLimit_AllowsEverything()
    {
        var adapter = new PaymentAttemptLimiter(Mock.Of<IAttemptLimiter>(), Options.Create(new PaymentRateLimitOptions { PermitLimit = 0 }));

        Assert.True(await adapter.AcquireAsync("setup-intent", "1.2.3.4"));
    }
}

#pragma warning restore CS0618
