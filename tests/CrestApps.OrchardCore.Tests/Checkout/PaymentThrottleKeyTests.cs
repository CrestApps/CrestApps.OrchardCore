using System.Net;
using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using CrestApps.OrchardCore.Checkout.Api;
using CrestApps.OrchardCore.Checkout.Endpoints;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// RL-0 (SEC-C7): a card tester who starts a new checkout per batch must not get a fresh budget of payment attempts
/// each time, so begin-payment is counted per caller across every checkout. Status polling stays per checkout, so a
/// buyer polling one checkout is not throttled by another.
/// </summary>
public sealed class PaymentThrottleKeyTests
{
    [Fact]
    public void BeginPaymentKey_IsTheSameForEveryCheckoutOfOneCaller()
    {
        // Two requests from one caller, for two checkouts.
        var first = CreateContext("203.0.113.7");
        var second = CreateContext("203.0.113.7");
        first.Request.RouteValues["sessionId"] = "session-a";
        second.Request.RouteValues["sessionId"] = "session-b";

        Assert.Equal(PaymentEndpoints.GetBeginPaymentKey(first), PaymentEndpoints.GetBeginPaymentKey(second));
        Assert.Equal(PaymentEndpoints.GetBeginPaymentKey(first), CheckoutApiEndpoints.GetBeginPaymentKey(second));
    }

    /// <summary>
    /// RL-0 (review): the plan's test. A caller who begins one payment in each of many checkouts is refused once the
    /// per-network budget is spent, by the begin endpoint itself, before any checkout is looked up.
    /// </summary>
    [Fact]
    public async Task BeginningAPaymentInEachOfManyCheckouts_IsRefusedOnceTheNetworksBudgetIsSpent()
    {
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.PaymentBeginNetwork, 30, TimeSpan.FromMinutes(10));

        var limiter = new DistributedAttemptLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = "Default" },
            Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow),
            Options.Create(options),
            NullLogger<DistributedAttemptLimiter>.Instance);

        // No checkout is found, so a begin that gets past the limit answers 404.
        var sessions = Mock.Of<ICheckoutSessionStore>();
        var begin = typeof(PaymentEndpoints).GetMethod("BeginAsync", BindingFlags.NonPublic | BindingFlags.Static);
        var statuses = new List<int>();

        for (var i = 0; i < 31; i++)
        {
            var httpContext = CreateContext("203.0.113.7");
            httpContext.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

            var result = await (Task<IResult>)begin.Invoke(null, ["session-" + i, new PaymentEndpoints.BeginPaymentRequest { ProviderKey = "card" }, httpContext, sessions, Mock.Of<ICheckoutEngine>(), limiter, CancellationToken.None]);

            statuses.Add(((IStatusCodeHttpResult)result).StatusCode ?? 0);
        }

        Assert.All(statuses.Take(30), status => Assert.Equal(StatusCodes.Status404NotFound, status));
        Assert.Equal(StatusCodes.Status429TooManyRequests, statuses[^1]);
    }

    [Fact]
    public void StatusKey_StillSeparatesCheckouts()
    {
        var httpContext = CreateContext("203.0.113.7");

        Assert.NotEqual(
            PaymentEndpoints.GetStatusKey(httpContext, "session-a"),
            PaymentEndpoints.GetStatusKey(httpContext, "session-b"));
    }

    [Fact]
    public void BeginPaymentKey_DiffersByCaller()
    {
        Assert.NotEqual(
            PaymentEndpoints.GetBeginPaymentKey(CreateContext("203.0.113.7")),
            PaymentEndpoints.GetBeginPaymentKey(CreateContext("203.0.113.8")));
    }

    private static DefaultHttpContext CreateContext(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        return context;
    }
}
