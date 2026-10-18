using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// A checkout with a coupon is taxed on what the buyer pays: the discounted items, and shipping as it is.
/// </summary>
public sealed class CheckoutDiscountTaxTests
{
    [Fact]
    public async Task ACouponOrder_IsTaxedOnTheDiscountedItems_AndOnTheShipping()
    {
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        var washington = await TaxTestData.AddJurisdictionAsync(harness, "Washington", "US", "WA");
        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Washington sales tax",
            TaxType = TaxTypeNames.SalesTax,
            Source = "Percentage",
            JurisdictionId = washington,
            Rate = 0.10m,
            AppliesToShipping = true,
        });

        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 65m,
            DueNow = 65m,
            LineItems =
            [
                new CheckoutLineItem { ItemId = "watch", Quantity = 1, UnitPrice = 50m, TaxableKind = TaxableItemKind.Physical },
                new CheckoutLineItem { ItemId = "shipping", Quantity = 1, UnitPrice = 15m, TaxableKind = TaxableItemKind.Shipping },
            ],
        };

        var discounts = new DefaultCheckoutDiscountService([new TenPercentOff()], NullLogger<DefaultCheckoutDiscountService>.Instance);
        await discounts.ApplyDiscountsAsync(invoice, flow: null, TestContext.Current.CancellationToken);

        var profile = new CheckoutTaxProfile { Destination = new Address { Country = "US", Region = "WA", City = "Seattle", PostalCode = "98101", AddressLine1 = "400 Pine St" } };
        var context = CheckoutTaxContextFactory.Create(invoice, profile, TaxTestData.TransactionDate);

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // $45 of the watch after 10% off, and $15 of shipping, each taxed at 10%.
        Assert.Equal(4.50m, result.Lines.Where(line => line.ItemId == "watch").Sum(line => line.TaxAmount));
        Assert.Equal(1.50m, result.Lines.Where(line => line.ItemId == "shipping").Sum(line => line.TaxAmount));
        Assert.Equal(6.00m, result.TaxAmount);
    }

    private sealed class TenPercentOff : ICheckoutDiscountProvider
    {
        public string Key => "test";

        public Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DiscountLine>>(
            [
                new DiscountLine
                {
                    Code = "SAVE10",
                    Amount = CheckoutDiscountBase.GetAmount(context.Invoice, DiscountTarget.OneTime) * 0.10m,
                    Target = DiscountTarget.OneTime,
                },
            ]);

        public Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
