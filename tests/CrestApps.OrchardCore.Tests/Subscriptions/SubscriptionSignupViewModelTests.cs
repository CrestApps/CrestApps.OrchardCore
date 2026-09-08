using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Whether the plan card asks the visitor anything before checkout starts.
/// </summary>
/// <remarks>
/// Getting this wrong is not cosmetic in either direction: a form where there is nothing to choose adds a
/// step to every purchase, and a plain button where there *is* a choice quietly sells the default — the
/// visitor never sees the annual price they came for.
/// </remarks>
public sealed class SubscriptionSignupViewModelTests
{
    /// <summary>
    /// One fixed price needs no form; sending them straight into the checkout cannot be got wrong.
    /// </summary>
    [Fact]
    public void RequiresChoice_ForOneFixedPrice_IsFalse()
    {
        var price = Price("monthly");

        Assert.False(new SubscriptionSignupViewModel { Prices = [price], DefaultPrice = price }.RequiresChoice);
    }

    /// <summary>
    /// A plan with no prices of its own is sold at its product part's amount, which is also no choice.
    /// </summary>
    [Fact]
    public void RequiresChoice_ForNoPrices_IsFalse()
        => Assert.False(new SubscriptionSignupViewModel().RequiresChoice);

    /// <summary>
    /// Two prices is a choice, and it has to be shown.
    /// </summary>
    [Fact]
    public void RequiresChoice_ForSeveralPrices_IsTrue()
    {
        var first = Price("monthly");
        var second = Price("annual");

        Assert.True(new SubscriptionSignupViewModel { Prices = [first, second], DefaultPrice = first }.RequiresChoice);
    }

    /// <summary>
    /// Pay-what-you-want is a choice even when it is the only price: without the field the visitor can
    /// never name their amount.
    /// </summary>
    [Fact]
    public void RequiresChoice_ForABuyerNamedAmount_IsTrue()
    {
        var price = Price("supporter");
        price.AllowCustomAmount = true;

        Assert.True(new SubscriptionSignupViewModel { Prices = [price], DefaultPrice = price }.RequiresChoice);
    }

    /// <summary>
    /// So is a quantity: a seat plan sold one seat at a time is not the plan.
    /// </summary>
    [Fact]
    public void RequiresChoice_ForAQuantity_IsTrue()
    {
        var price = Price("seats");
        price.AllowQuantity = true;

        Assert.True(new SubscriptionSignupViewModel { Prices = [price], DefaultPrice = price }.RequiresChoice);
    }

    private static ProductPrice Price(string id)
        => new()
        {
            PriceId = id,
            Name = id,
            Currency = "USD",
            Amount = 10m,
            Kind = PriceKind.Recurring,
            BillingDuration = 1,
            Interval = BillingInterval.Month,
            IsActive = true,
        };
}
