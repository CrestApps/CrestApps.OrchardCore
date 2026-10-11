using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Discount arithmetic is money arithmetic, so these tests pin the rules that stop a coupon costing more
/// than it was meant to: a total never goes below zero, a one-time coupon never eats a recurring charge,
/// and what is recorded always equals what was actually taken off.
/// </summary>
public sealed class CheckoutDiscountTests
{
    [Fact]
    public async Task ApplyDiscountsAsync_ReducesTheOneTimeAmountAndTheTotal()
    {
        // Arrange
        var invoice = CreateInvoice(oneTime: 100m, firstCycle: 0m);
        var service = CreateService(new DiscountLine { Code = "TEN", Amount = 10m, Target = DiscountTarget.OneTime });

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(90m, invoice.InitialPaymentAmount);
        Assert.Equal(90m, invoice.DueNow);
        Assert.Equal(10m, invoice.DiscountTotal);
    }

    /// <summary>
    /// A coupon worth more than the basket makes the purchase free. It does not make the site owe the
    /// customer money.
    /// </summary>
    [Fact]
    public async Task ApplyDiscountsAsync_NeverTakesATotalBelowZero()
    {
        // Arrange
        var invoice = CreateInvoice(oneTime: 20m, firstCycle: 0m);
        var service = CreateService(new DiscountLine { Code = "BIG", Amount = 500m, Target = DiscountTarget.OneTime });

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0m, invoice.InitialPaymentAmount);
        Assert.Equal(0m, invoice.DueNow);

        // What is recorded is what was actually taken off, so a receipt cannot show a discount larger than
        // the price it applied to.
        Assert.Equal(20m, invoice.DiscountTotal);
    }

    /// <summary>
    /// "First month half price" must not silently halve every month after, and a setup-fee coupon must not
    /// reduce the recurring charge the customer agreed to.
    /// </summary>
    [Fact]
    public async Task ApplyDiscountsAsync_KeepsTheBucketsSeparate()
    {
        // Arrange
        var invoice = CreateInvoice(oneTime: 50m, firstCycle: 30m);

        var service = CreateService(
            new DiscountLine { Code = "SETUP", Amount = 500m, Target = DiscountTarget.OneTime },
            new DiscountLine { Code = "FIRST", Amount = 10m, Target = DiscountTarget.FirstCycle });

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0m, invoice.InitialPaymentAmount);
        Assert.Equal(20m, invoice.FirstRecurringPaymentAmount);
        Assert.Equal(20m, invoice.DueNow);
    }

    /// <summary>
    /// When two coupons together exceed what the bucket can absorb, the recorded lines are scaled down so
    /// their sum still equals what was taken off, and rounding never invents or loses a cent.
    /// </summary>
    [Fact]
    public async Task ApplyDiscountsAsync_ScalesRecordedLinesToWhatWasActuallyApplied()
    {
        // Arrange
        var invoice = CreateInvoice(oneTime: 30m, firstCycle: 0m);

        var service = CreateService(
            new DiscountLine { Code = "A", Amount = 20m, Target = DiscountTarget.OneTime },
            new DiscountLine { Code = "B", Amount = 40m, Target = DiscountTarget.OneTime });

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0m, invoice.InitialPaymentAmount);
        Assert.Equal(30m, invoice.DiscountTotal);
        Assert.Equal(2, invoice.Discounts.Count);
    }

    [Fact]
    public async Task ApplyDiscountsAsync_IgnoresANonPositiveDiscount()
    {
        // Arrange
        var invoice = CreateInvoice(oneTime: 100m, firstCycle: 0m);
        var service = CreateService(new DiscountLine { Code = "ZERO", Amount = 0m, Target = DiscountTarget.OneTime });

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(100m, invoice.InitialPaymentAmount);
        Assert.Empty(invoice.Discounts);
    }

    /// <summary>
    /// A discount provider that throws must not stop somebody buying. Full price is recoverable; a failed
    /// checkout is a lost sale.
    /// </summary>
    [Fact]
    public async Task ApplyDiscountsAsync_WhenAProviderThrows_ContinuesAtFullPrice()
    {
        // Arrange
        var invoice = CreateInvoice(oneTime: 100m, firstCycle: 0m);

        var service = new DefaultCheckoutDiscountService(
            [new ThrowingProvider()],
            NullLogger<DefaultCheckoutDiscountService>.Instance);

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(100m, invoice.InitialPaymentAmount);
        Assert.Empty(invoice.Discounts);
    }

    /// <summary>
    /// Rebuilding the invoice must not stack a discount on top of itself. The invoice is recomputed on
    /// every step, so a coupon that accumulated would eventually make everything free.
    /// </summary>
    [Fact]
    public async Task ApplyDiscountsAsync_AppliedTwice_DoesNotStack()
    {
        // Arrange
        var service = CreateService(new DiscountLine { Code = "TEN", Amount = 10m, Target = DiscountTarget.OneTime });

        var invoice = CreateInvoice(oneTime: 100m, firstCycle: 0m);

        // Act
        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // The invoice is rebuilt from the line items on every step, which is what the handler does.
        invoice = CreateInvoice(oneTime: 100m, firstCycle: 0m);

        await service.ApplyDiscountsAsync(invoice, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(90m, invoice.InitialPaymentAmount);
        Assert.Single(invoice.Discounts);
    }

    private static CheckoutInvoice CreateInvoice(decimal oneTime, decimal firstCycle)
        => new()
        {
            Currency = "USD",
            InitialPaymentAmount = oneTime,
            FirstRecurringPaymentAmount = firstCycle > 0m ? firstCycle : null,
            DueNow = oneTime + firstCycle,
            GrandTotal = oneTime + firstCycle,
            LineItems = [],
        };

    private static DefaultCheckoutDiscountService CreateService(params DiscountLine[] discounts)
        => new([new StaticProvider(discounts)], NullLogger<DefaultCheckoutDiscountService>.Instance);

    private sealed class StaticProvider : ICheckoutDiscountProvider
    {
        private readonly DiscountLine[] _discounts;

        public StaticProvider(DiscountLine[] discounts)
            => _discounts = discounts;

        public string Key => "test";

        public Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscountLine>>(
                [.. _discounts.Select(discount => new DiscountLine
                {
                    ProviderKey = discount.ProviderKey,
                    Code = discount.Code,
                    Description = discount.Description,
                    Amount = discount.Amount,
                    Target = discount.Target,
                })]);

        public Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingProvider : ICheckoutDiscountProvider
    {
        public string Key => "broken";

        public Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The promotion service is unavailable.");

        public Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

/// <summary>
/// The coupon rules that decide whether a code costs the site owner more than it was meant to.
/// </summary>
public sealed class CouponTests
{
    private static readonly DateTime _now = new(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetDiscount_ForAPercentage_TakesThatShareOff()
    {
        // Arrange
        var coupon = new Coupon { Kind = CouponKind.Percentage, Percentage = 25m };

        // Act & Assert
        Assert.Equal(25m, coupon.GetDiscount(100m, "USD"));
    }

    /// <summary>
    /// Ten dollars off is not ten euros off, and guessing a rate would charge the customer something nobody
    /// chose.
    /// </summary>
    [Fact]
    public void GetDiscount_ForAFixedAmountInAnotherCurrency_TakesNothingOff()
    {
        // Arrange
        var coupon = new Coupon { Kind = CouponKind.FixedAmount, Amount = 10m, Currency = "USD" };

        // Act & Assert
        Assert.Equal(10m, coupon.GetDiscount(100m, "USD"));
        Assert.Equal(0m, coupon.GetDiscount(100m, "EUR"));
    }

    [Fact]
    public void GetDiscount_BelowTheMinimum_TakesNothingOff()
    {
        // Arrange
        var coupon = new Coupon { Kind = CouponKind.Percentage, Percentage = 50m, MinimumAmount = 100m };

        // Act & Assert
        Assert.Equal(0m, coupon.GetDiscount(99m, "USD"));
        Assert.Equal(50m, coupon.GetDiscount(100m, "USD"));
    }

    /// <summary>
    /// A code with no usage limit that leaks is unbounded liability, so the limit is enforced rather than
    /// merely recorded.
    /// </summary>
    [Fact]
    public void IsRedeemable_WhenTheUsageLimitIsReached_IsFalse()
    {
        // Arrange
        var coupon = new Coupon { MaxRedemptions = 2, RedemptionCount = 2 };

        // Act & Assert
        Assert.False(coupon.IsRedeemable(_now));
    }

    [Theory]
    [InlineData(-10, -5, false)]
    [InlineData(-10, 5, true)]
    [InlineData(5, 10, false)]
    public void IsRedeemable_HonorsTheValidityWindow(int startsOffsetDays, int endsOffsetDays, bool expected)
    {
        // Arrange
        var coupon = new Coupon
        {
            StartsUtc = _now.AddDays(startsOffsetDays),
            EndsUtc = _now.AddDays(endsOffsetDays),
        };

        // Act & Assert
        Assert.Equal(expected, coupon.IsRedeemable(_now));
    }

    [Fact]
    public void IsRedeemable_WhenDisabled_IsFalse()
    {
        // Arrange
        var coupon = new Coupon { IsEnabled = false };

        // Act & Assert
        Assert.False(coupon.IsRedeemable(_now));
    }

    /// <summary>
    /// A percentage outside nought to a hundred is a configuration mistake. Clamping it means a typo cannot
    /// pay the customer or charge them more.
    /// </summary>
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    public void GetDiscount_ClampsAnOutOfRangePercentage(decimal percentage, decimal expected)
    {
        // Arrange
        var coupon = new Coupon { Kind = CouponKind.Percentage, Percentage = percentage };

        // Act & Assert
        Assert.Equal(expected, coupon.GetDiscount(100m, "USD"));
    }
}
