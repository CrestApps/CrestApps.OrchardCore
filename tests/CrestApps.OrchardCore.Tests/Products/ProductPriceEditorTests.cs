using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A price saved wrong is not caught by the person saving it; it is caught by the next customer who tries
/// to buy, or worse, by the one who succeeds on the wrong terms. These are the rules that stop an
/// incoherent price reaching the catalog at all.
/// </summary>
public sealed class ProductPriceEditorTests
{
    /// <summary>
    /// The editor always offers a row to type into, and an untouched one is not an attempt to add a price.
    /// </summary>
    [Fact]
    public void IsBlank_ForAnUntouchedRow_IsTrue()
    {
        Assert.True(ProductPriceEditor.IsBlank(null, null));
        Assert.True(ProductPriceEditor.IsBlank(null, string.Empty));
        Assert.True(ProductPriceEditor.IsBlank(null, "   "));
    }

    /// <summary>
    /// Anything typed in makes it a real price, so it gets validated rather than silently dropped.
    /// </summary>
    [Fact]
    public void IsBlank_ForATouchedRow_IsFalse()
    {
        // A zero amount counts: "free" is a price somebody chose to offer.
        Assert.False(ProductPriceEditor.IsBlank(0m, null));
        Assert.False(ProductPriceEditor.IsBlank(10m, null));
        Assert.False(ProductPriceEditor.IsBlank(null, "Monthly"));
    }

    /// <summary>
    /// A free price is legitimate; a negative one is somebody being paid to take the product.
    /// </summary>
    [Fact]
    public void Validate_AllowsZero_AndRefusesNegative()
    {
        Assert.Empty(ProductPriceEditor.Validate(Recurring(amount: 0m)));

        Assert.Contains(
            ProductPriceEditor.Validate(Recurring(amount: -1m)),
            error => error.Kind == ProductPriceErrorKind.NegativeAmount);
    }

    /// <summary>
    /// A recurring price with no schedule cannot be sold: there is nothing to tell a gateway about how
    /// often to bill it.
    /// </summary>
    [Fact]
    public void Validate_ForARecurringPriceWithNoInterval_Refuses()
    {
        var price = Recurring();
        price.Interval = null;

        Assert.Contains(
            ProductPriceEditor.Validate(price),
            error => error.Kind == ProductPriceErrorKind.MissingInterval);
    }

    /// <summary>
    /// Every cycle has to be at least one interval long.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ForARecurringPriceWithNoDuration_Refuses(int? duration)
    {
        var price = Recurring();
        price.BillingDuration = duration;

        Assert.Contains(
            ProductPriceEditor.Validate(price),
            error => error.Kind == ProductPriceErrorKind.MissingBillingDuration);
    }

    /// <summary>
    /// A one-time price needs no schedule, so the same fields being empty is not a problem.
    /// </summary>
    [Fact]
    public void Validate_ForAOneTimePrice_NeedsNoSchedule()
    {
        var price = Recurring();
        price.Kind = PriceKind.OneTime;
        price.Interval = null;
        price.BillingDuration = null;

        Assert.Empty(ProductPriceEditor.Validate(price));
    }

    /// <summary>
    /// A pay-what-you-want band that excludes every amount would refuse every purchase.
    /// </summary>
    [Fact]
    public void Validate_WhenTheMinimumIsAboveTheMaximum_Refuses()
    {
        var price = Recurring();
        price.AllowCustomAmount = true;
        price.MinimumAmount = 100m;
        price.MaximumAmount = 10m;

        Assert.Contains(
            ProductPriceEditor.Validate(price),
            error => error.Kind == ProductPriceErrorKind.MinimumAboveMaximum);
    }

    /// <summary>
    /// The same bounds on a fixed price are not used, so they are not worth refusing a save over.
    /// </summary>
    [Fact]
    public void Validate_WhenTheBoundsDoNotApply_IgnoresThem()
    {
        var price = Recurring();
        price.AllowCustomAmount = false;
        price.MinimumAmount = 100m;
        price.MaximumAmount = 10m;

        Assert.Empty(ProductPriceEditor.Validate(price));
    }

    /// <summary>
    /// A negative trial would bill the customer before they bought.
    /// </summary>
    [Fact]
    public void Validate_ForANegativeTrial_Refuses()
    {
        var price = Recurring();
        price.TrialDays = -1;

        Assert.Contains(
            ProductPriceEditor.Validate(price),
            error => error.Kind == ProductPriceErrorKind.NegativeTrial);
    }

    /// <summary>
    /// An offer window that closes before it opens is never on offer.
    /// </summary>
    [Fact]
    public void Validate_WhenTheOfferEndsBeforeItStarts_Refuses()
    {
        var price = Recurring();
        price.EffectiveFromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        price.EffectiveToUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Contains(
            ProductPriceEditor.Validate(price),
            error => error.Kind == ProductPriceErrorKind.EndsBeforeItStarts);
    }

    /// <summary>
    /// A link straight to "buy" names no price, so a set that marks none still has to resolve to one.
    /// </summary>
    [Fact]
    public void SettleDefault_WhenNoneIsMarked_PromotesTheFirst()
    {
        var prices = new List<ProductPrice> { Recurring(), Recurring() };

        Assert.Null(ProductPriceEditor.SettleDefault(prices));
        Assert.True(prices[0].IsDefault);
        Assert.False(prices[1].IsDefault);
    }

    /// <summary>
    /// Two defaults is a coin toss over what the customer is charged, so it is refused rather than settled.
    /// </summary>
    [Fact]
    public void SettleDefault_WhenSeveralAreMarked_Refuses()
    {
        var first = Recurring();
        var second = Recurring();

        first.IsDefault = true;
        second.IsDefault = true;

        var error = ProductPriceEditor.SettleDefault([first, second]);

        Assert.NotNull(error);
        Assert.Equal(ProductPriceErrorKind.SeveralDefaults, error.Kind);
    }

    /// <summary>
    /// A deliberate default is left alone.
    /// </summary>
    [Fact]
    public void SettleDefault_WhenOneIsMarked_LeavesIt()
    {
        var first = Recurring();
        var second = Recurring();

        second.IsDefault = true;

        Assert.Null(ProductPriceEditor.SettleDefault([first, second]));
        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);
    }

    /// <summary>
    /// A product with no prices at all is sold at its product part's amount, so there is nothing to settle.
    /// </summary>
    [Fact]
    public void SettleDefault_ForNoPrices_DoesNothing()
        => Assert.Null(ProductPriceEditor.SettleDefault([]));

    private static ProductPrice Recurring(decimal amount = 10m)
        => new()
        {
            PriceId = "price-1",
            Name = "Monthly",
            Currency = "USD",
            Amount = amount,
            Kind = PriceKind.Recurring,
            BillingDuration = 1,
            Interval = BillingInterval.Month,
            IsActive = true,
        };
}
