using CrestApps.OrchardCore.Stripe.Core;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class StripeIdempotencyKeyTests
{
    [Fact]
    public void Compute_IsDeterministic_ForSameInputs()
    {
        var a = StripeIdempotencyKey.Compute("sub_pi", "session-1", "cus_1", "pm_1", "1000", "usd");
        var b = StripeIdempotencyKey.Compute("sub_pi", "session-1", "cus_1", "pm_1", "1000", "usd");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Compute_Differs_WhenAnyParameterChanges()
    {
        var baseKey = StripeIdempotencyKey.Compute("sub_pi", "session-1", "cus_1", "pm_1", "1000", "usd");

        Assert.NotEqual(baseKey, StripeIdempotencyKey.Compute("sub_pi", "session-1", "cus_1", "pm_2", "1000", "usd"));
        Assert.NotEqual(baseKey, StripeIdempotencyKey.Compute("sub_pi", "session-1", "cus_1", "pm_1", "1500", "usd"));
        Assert.NotEqual(baseKey, StripeIdempotencyKey.Compute("sub_pi", "session-2", "cus_1", "pm_1", "1000", "usd"));
    }

    [Fact]
    public void Compute_Differs_ByScope()
    {
        var pi = StripeIdempotencyKey.Compute("sub_pi", "session-1");
        var si = StripeIdempotencyKey.Compute("sub_si", "session-1");

        Assert.NotEqual(pi, si);
    }

    [Fact]
    public void Compute_PrefixesWithScope_AndStaysWithinStripeLimit()
    {
        var key = StripeIdempotencyKey.Compute("sub_cs", "session-1", "price_1:1");

        Assert.StartsWith("sub_cs_", key);
        Assert.True(key.Length <= 255);
    }

    [Fact]
    public void Compute_TreatsNullAndEmptyPartsConsistently()
    {
        var withNull = StripeIdempotencyKey.Compute("sub_pi", "session-1", null, "usd");
        var withEmpty = StripeIdempotencyKey.Compute("sub_pi", "session-1", "", "usd");

        Assert.Equal(withNull, withEmpty);
    }

    [Fact]
    public void Compute_IsOrderSensitive()
    {
        var first = StripeIdempotencyKey.Compute("sub_pi", "a", "b");
        var second = StripeIdempotencyKey.Compute("sub_pi", "b", "a");

        Assert.NotEqual(first, second);
    }
}
