using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Review 11 (M-agent-coupon-bucket): an agent request with no client address (MCP, A2A) counted its wrong discount codes
/// in one shared "unknown" bucket, so one agent platform that guessed codes locked every platform out of codes. Its tries
/// are now counted per platform, through the real coupon lookup and attempt limiter.
/// </summary>
public sealed class AgentClientKeyTests
{
    [Fact]
    public async Task WrongCodesFromOnePlatform_DontLockAnotherPlatformOutOfCodes()
    {
        // Arrange
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.CouponCode, 10, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        var limiter = new DistributedAttemptLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = "Default" },
            Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow),
            Options.Create(options),
            NullLogger<DistributedAttemptLimiter>.Instance);

        var coupons = new CheckoutCouponService(
            Mock.Of<ICouponStore>(),
            null,
            Mock.Of<ICouponRedemptionService>(),
            limiter,
            null,
            Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow),
            new LocalLock(NullLogger<LocalLock>.Instance),
            new PassThroughStringLocalizer<CheckoutCouponService>());

        // Over MCP and A2A the request names no client address.
        var guesser = new AgentCheckoutContext { PlatformId = "platform-guesser" }.GetClientKey();
        var other = new AgentCheckoutContext { PlatformId = "platform-other" }.GetClientKey();

        for (var i = 0; i < 10; i++)
        {
            await coupons.FindCodeAsync("WRONG" + i, guesser);
        }

        // Act
        var (_, guesserRefused) = await coupons.FindCodeAsync("WRONG-AGAIN", guesser);
        var (_, otherRefused) = await coupons.FindCodeAsync("SAVE10", other);

        // Assert
        Assert.True(guesserRefused);
        Assert.False(otherRefused);
        Assert.NotEqual(ClientNetworkKey.Unknown, ClientNetworkKey.FromAddress(guesser));
    }

    [Fact]
    public void AnAgentRequestWithoutAPlatform_IsKeyedByItsAddress()
        => Assert.Equal("203.0.113.7", new AgentCheckoutContext { ClientAddress = "203.0.113.7" }.GetClientKey());
}
