using System.Net;
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
        var httpContext = CreateContext("203.0.113.7");

        Assert.Equal(
            PaymentEndpoints.GetBeginPaymentKey(httpContext),
            PaymentEndpoints.GetBeginPaymentKey(httpContext));

        Assert.DoesNotContain("session", PaymentEndpoints.GetBeginPaymentKey(httpContext), StringComparison.Ordinal);
        Assert.Equal(PaymentEndpoints.GetBeginPaymentKey(httpContext), CheckoutApiEndpoints.GetBeginPaymentKey(httpContext));
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
