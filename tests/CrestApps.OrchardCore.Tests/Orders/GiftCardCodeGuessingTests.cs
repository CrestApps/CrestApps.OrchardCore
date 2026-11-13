using System.Net;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// RL-1 and RL-5 (review): wrong gift card codes are limited per network. Every IPv6 shopper shared one bucket, so five
/// wrong codes from any IPv6 client blocked gift cards for all of them, and parallel tries all passed the check before
/// any wrong code was counted.
/// </summary>
public sealed class GiftCardCodeGuessingTests
{
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IDistributedLock _locks = new LocalLock(NullLogger<LocalLock>.Instance);
    private int _lookups;

    [Fact]
    public async Task ApplyAsync_WrongCodesFromOneIPv6Network_DoNotBlockAnotherIPv6Network()
    {
        var service = CreateService();
        var tester = Network("2001:db8:1:1::10");
        var shopper = Network("2001:db8:2:2::20");

        for (var i = 0; i < 5; i++)
        {
            await service.ApplyAsync(Flow(), "WRONG" + i, tester);
        }

        Assert.Equal("Too many codes were tried. Wait a few minutes, then try again.", await service.ApplyAsync(Flow(), "WRONG-X", tester));
        Assert.Equal("That gift card code is not recognized.", await service.ApplyAsync(Flow(), "WRONG-Y", shopper));
    }

    [Fact]
    public async Task ApplyAsync_ParallelWrongCodesFromOneNetwork_LookUpNoMoreThanTheLimit()
    {
        var service = CreateService(yieldOnLookup: true);
        var network = Network("203.0.113.7");

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => service.ApplyAsync(Flow(), "WRONG" + i, network)));

        Assert.Equal(5, _lookups);
    }

    private static string Network(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return ClientNetworkKey.NetworkOnly(context);
    }

    private static CheckoutFlow Flow()
        => new(new CheckoutSession { SessionId = "session-1", Currency = "USD" });

    private GiftCardCheckoutService CreateService(bool yieldOnLookup = false)
    {
        var store = new Mock<IGiftCardStore>();
        store.Setup(value => value.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken _) =>
            {
                Interlocked.Increment(ref _lookups);

                if (yieldOnLookup)
                {
                    await Task.Yield();
                }

                return (GiftCard)null;
            });

        var options = new AttemptLimitOptions();
        options.AddPolicy(OrderConstants.AttemptPolicies.GiftCardCode, 5, TimeSpan.FromMinutes(10), blockDuration: TimeSpan.FromMinutes(30));

        var clock = Mock.Of<IClock>(value => value.UtcNow == DateTime.UtcNow);
        var limiter = new DistributedAttemptLimiter(_cache, _locks, new ShellSettings { Name = "Default" }, clock, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        var localizer = new Mock<IStringLocalizer<GiftCardCheckoutService>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        var services = new ServiceCollection()
            .AddSingleton(store.Object)
            .AddSingleton<IAttemptLimiter>(limiter)
            .AddSingleton(_locks)
            .AddSingleton(clock)
            .AddSingleton(localizer.Object)
            .BuildServiceProvider();

        // The invoice builder isn't reached by a code that isn't found.
        return ActivatorUtilities.CreateInstance<GiftCardCheckoutService>(
            services,
            (CheckoutInvoiceBuilder)RuntimeHelpers.GetUninitializedObject(typeof(CheckoutInvoiceBuilder)));
    }
}
