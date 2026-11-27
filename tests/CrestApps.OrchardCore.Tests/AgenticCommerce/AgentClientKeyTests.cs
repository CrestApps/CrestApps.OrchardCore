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
        options.AddPolicy(CheckoutConstants.AttemptPolicies.CouponCodeAgentPlatform, 100, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

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
        var guesser = new AgentCheckoutContext { PlatformId = "platform-guesser" }.GetClientKey("checkout-1");
        var other = new AgentCheckoutContext { PlatformId = "platform-other" }.GetClientKey("checkout-2");
        var sameBuyer = new AgentCheckoutContext { PlatformId = "platform-guesser" }.GetClientKey("checkout-1");
        var anotherBuyerOfThePlatform = new AgentCheckoutContext { PlatformId = "platform-guesser" }.GetClientKey("checkout-3");

        for (var i = 0; i < 10; i++)
        {
            await coupons.FindCodeAsync("WRONG" + i, guesser);
        }

        // Act
        var (_, guesserRefused) = await coupons.FindCodeAsync("WRONG-AGAIN", sameBuyer);
        var (_, otherRefused) = await coupons.FindCodeAsync("SAVE10", other);
        var (_, anotherBuyerRefused) = await coupons.FindCodeAsync("SAVE10", anotherBuyerOfThePlatform);

        // Assert: round 2, #3 keeps one buyer's wrong codes from locking every buyer of the same platform out of codes.
        Assert.True(guesserRefused);
        Assert.False(otherRefused);
        Assert.False(anotherBuyerRefused);
        Assert.NotEqual(ClientNetworkKey.Unknown, ClientNetworkKey.FromAddress(guesser));
    }

    [Fact]
    public async Task WrongCodesAcrossAPlatformsBuyers_StillStopThePlatform_AtItsOwnLimit()
    {
        // Arrange
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.CouponCode, 10, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        options.AddPolicy(CheckoutConstants.AttemptPolicies.CouponCodeAgentPlatform, 30, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

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

        // A guesser that opens a new checkout for every few tries.
        for (var i = 0; i < 30; i++)
        {
            await coupons.FindCodeAsync("WRONG" + i, new AgentCheckoutContext { PlatformId = "platform" }.GetClientKey("checkout-" + (i / 5)));
        }

        // Act
        var (_, refused) = await coupons.FindCodeAsync("SAVE10", new AgentCheckoutContext { PlatformId = "platform" }.GetClientKey("checkout-new"));

        // Assert
        Assert.True(refused);
    }

    [Fact]
    public void TwoIpv6NetworksOfOneSite_ShareTheSitesKey()
    {
        // Round 2 (Mild): an IPv6 /48 holder multiplied the address suggestions' daily share by its 65,536 /64 networks.
        static string SiteOf(string address)
            => ClientNetworkKey.Site(new Microsoft.AspNetCore.Http.DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse(address) } });

        Assert.Equal(SiteOf("2001:db8:1:1::5"), SiteOf("2001:db8:1:ffff::9"));
        Assert.NotEqual(SiteOf("2001:db8:1:1::5"), SiteOf("2001:db8:2:1::5"));
        Assert.Equal("203.0.113.7", SiteOf("203.0.113.7"));
    }

    [Fact]
    public void AnAgentBuyersKey_NamesItsPlatform()
    {
        var key = ClientNetworkKey.ForAgentBuyer("platform:x", "checkout-1");

        Assert.Equal(ClientNetworkKey.ForAgentPlatform("platform:x"), ClientNetworkKey.GetAgentPlatform(key));
        Assert.Null(ClientNetworkKey.GetAgentPlatform(ClientNetworkKey.ForAgentPlatform("platform:x")));
        Assert.Null(ClientNetworkKey.GetAgentPlatform("203.0.113.7"));
    }

    [Fact]
    public void AnAgentRequestWithoutAPlatform_IsKeyedByItsAddress()
        => Assert.Equal("203.0.113.7", new AgentCheckoutContext { ClientAddress = "203.0.113.7" }.GetClientKey("checkout-1"));
}
