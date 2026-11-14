using System.Net;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Controllers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// RL-1, RL-5 and SEC-C17a (review): wrong promotion codes are limited per network, through the code box and the
/// discount link alike. Every IPv6 shopper shared one bucket, parallel tries all passed the check before any wrong code
/// was counted, and the discount link never counted at all.
/// </summary>
public sealed class CouponCodeGuessingTests
{
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IDistributedLock _locks = new LocalLock(NullLogger<LocalLock>.Instance);
    private readonly List<string> _lookups = [];
    private readonly List<(NotifyType Type, string Message)> _notices = [];

    [Fact]
    public async Task ApplyAsync_WrongCodesFromOneIPv6Network_DoNotBlockAnotherIPv6Network()
    {
        var service = CreateService();
        var tester = Network("2001:db8:1:1::10");
        var shopper = Network("2001:db8:2:2::20");

        for (var i = 0; i < 10; i++)
        {
            await service.ApplyAsync(Flow(), "WRONG" + i, tester);
        }

        Assert.Equal("Too many codes were tried. Wait a few minutes, then try again.", await service.ApplyAsync(Flow(), "WRONG-X", tester));
        Assert.Equal("That code is not recognized.", await service.ApplyAsync(Flow(), "WRONG-Y", shopper));
    }

    [Fact]
    public async Task ApplyAsync_ParallelWrongCodesFromOneNetwork_LookUpNoMoreThanTheLimit()
    {
        var service = CreateService(yieldOnLookup: true);
        var network = Network("203.0.113.7");

        await Task.WhenAll(Enumerable.Range(0, 25).Select(i => service.ApplyAsync(Flow(), "WRONG" + i, network)));

        Assert.Equal(10, _lookups.Count);
    }

    [Fact]
    public async Task TheDiscountLink_CountsWrongCodes_AndRefusesANetworkThatTriedTooMany()
    {
        var services = CreateServices(new Coupon { ItemId = "c1", Code = "SPRING", IsEnabled = true });
        var network = "198.51.100.9";

        for (var i = 0; i < 10; i++)
        {
            await CreateLinkController(services, network).Apply("GUESS" + i, redirect: null);
        }

        _notices.Clear();
        var controller = CreateLinkController(services, network);

        // A real code, followed after ten wrong ones from the same network.
        await controller.Apply("SPRING", redirect: null);

        var notice = Assert.Single(_notices);
        Assert.Equal(NotifyType.Warning, notice.Type);
        Assert.Equal("Too many codes were tried. Wait a few minutes, then try again.", notice.Message);
        Assert.False(controller.Response.Headers.ContainsKey("Set-Cookie"));
    }

    private static string Network(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return ClientNetworkKey.NetworkOnly(context);
    }

    private static CheckoutFlow Flow()
        => new(new CheckoutSession { SessionId = "session-1", Currency = "USD" });

    private CheckoutCouponService CreateService(bool yieldOnLookup = false)
        => CreateServices(yieldOnLookup: yieldOnLookup).GetRequiredService<CheckoutCouponService>();

    private ServiceProvider CreateServices(Coupon known = null, bool yieldOnLookup = false)
    {
        var store = new Mock<ICouponStore>();
        store.Setup(value => value.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string code, CancellationToken _) =>
            {
                lock (_lookups)
                {
                    _lookups.Add(code);
                }

                if (yieldOnLookup)
                {
                    await Task.Yield();
                }

                return known is not null && string.Equals(known.Code, code, StringComparison.OrdinalIgnoreCase) ? known : null;
            });

        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.CouponCode, 10, TimeSpan.FromMinutes(10), blockDuration: TimeSpan.FromMinutes(10));

        var clock = Mock.Of<IClock>(value => value.UtcNow == DateTime.UtcNow);
        var limiter = new DistributedAttemptLimiter(_cache, _locks, new ShellSettings { Name = "Default" }, clock, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);

        var notifier = new Mock<INotifier>();
        notifier.Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback<NotifyType, LocalizedHtmlString>((type, message) => _notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);
        notifier.Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
            .Callback<NotifyType, LocalizedHtmlString, NotifyContext>((type, message, _) => _notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);

        var localizer = new Mock<IStringLocalizer<CheckoutCouponService>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        var htmlLocalizer = new Mock<IHtmlLocalizer<DiscountLinkController>>();
        htmlLocalizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        htmlLocalizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        var services = new ServiceCollection()
            .AddSingleton(store.Object)
            .AddSingleton(Mock.Of<ICouponRedemptionService>())
            .AddSingleton<IAttemptLimiter>(limiter)
            .AddSingleton(_locks)
            .AddSingleton(clock)
            .AddSingleton(localizer.Object)
            .AddSingleton(htmlLocalizer.Object)
            .AddSingleton(notifier.Object);

        // The scope evaluator and invoice builder aren't reached by a code that isn't found.
        services.AddSingleton(provider => ActivatorUtilities.CreateInstance<CheckoutCouponService>(
            provider,
            (CouponScopeEvaluator)RuntimeHelpers.GetUninitializedObject(typeof(CouponScopeEvaluator)),
            (CheckoutInvoiceBuilder)RuntimeHelpers.GetUninitializedObject(typeof(CheckoutInvoiceBuilder))));

        return services.BuildServiceProvider();
    }

    private static DiscountLinkController CreateLinkController(IServiceProvider services, string address)
    {
        var controller = ActivatorUtilities.CreateInstance<DiscountLinkController>(services);
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);

        var url = new Mock<IUrlHelper>();
        url.Setup(helper => helper.IsLocalUrl(It.IsAny<string>())).Returns(true);
        url.Setup(helper => helper.Content(It.IsAny<string>())).Returns("/");

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = url.Object;

        return controller;
    }
}
