using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A cart checks out several products, some of them several times, each taxed as what it is. The checkout's
/// lines carry a quantity and the product's own tax details, and a line that carries none is billed and taxed
/// exactly as before.
/// </summary>
public sealed class CheckoutLineDetailsTests
{
    [Fact]
    public async Task BuildAsync_BillsEveryUnitOfALine()
    {
        var session = CreateSession(
            new BillingItem { ItemId = "mug", Description = "Mug", Amount = 12.50m, Quantity = 3, Sku = "MUG-1" },
            new BillingItem { ItemId = "book", Description = "Book", Amount = 20m });

        await CreateBuilder().BuildAsync(new CheckoutFlow(session));

        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(57.50m, invoice.DueNow);
        Assert.Equal(57.50m, invoice.InitialPaymentAmount);

        var mug = invoice.LineItems.Single(line => line.ItemId == "mug");

        Assert.Equal(3, mug.Quantity);
        Assert.Equal(12.50m, mug.UnitPrice);
        Assert.Equal("MUG-1", mug.Sku);
        Assert.Equal(1, invoice.LineItems.Single(line => line.ItemId == "book").Quantity);
    }

    [Fact]
    public async Task BuildAsync_TreatsAMissingQuantityAsOne()
    {
        var session = CreateSession(new BillingItem { ItemId = "fee", Description = "Fee", Amount = 5m, Quantity = 0 });

        await CreateBuilder().BuildAsync(new CheckoutFlow(session));

        Assert.True(session.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(5m, invoice.DueNow);
        Assert.Equal(1, Assert.Single(invoice.LineItems).Quantity);
    }

    [Fact]
    public void Create_TaxesALineAsWhatItSays_AndTheRestWithTheDefaults()
    {
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            LineItems =
            [
                new CheckoutLineItem
                {
                    ItemId = "ebook",
                    Quantity = 2,
                    UnitPrice = 9m,
                    TaxCategoryCode = "digital-books",
                    TaxClassificationCode = "DB",
                    TaxableKind = TaxableItemKind.Digital,
                },
                new CheckoutLineItem { ItemId = "lamp", Quantity = 1, UnitPrice = 40m },
            ],
        };

        var profile = new CheckoutTaxProfile { DefaultTaxCategoryCode = "general", DefaultTaxClassificationCode = "G" };

        var items = CheckoutTaxContextFactory.Create(invoice, profile, DateTime.UtcNow).Items;

        var ebook = items.Single(item => item.Id == "ebook");
        var lamp = items.Single(item => item.Id == "lamp");

        Assert.Equal(TaxableItemKind.Digital, ebook.Kind);
        Assert.Equal("digital-books", ebook.TaxCategoryCode);
        Assert.Equal("DB", ebook.TaxClassificationCode);
        Assert.Equal(2, ebook.Quantity);

        Assert.Equal(TaxableItemKind.Physical, lamp.Kind);
        Assert.Equal("general", lamp.TaxCategoryCode);
        Assert.Equal("G", lamp.TaxClassificationCode);
    }

    private static CheckoutSession CreateSession(params BillingItem[] items)
    {
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending, Currency = "USD" };

        session.Steps.Add(new CheckoutFlowStep { Key = "cart", Order = 0, BillingItems = items });

        return session;
    }

    private static CheckoutInvoiceBuilder CreateBuilder()
    {
        var siteService = new Mock<ISiteService>();
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new CheckoutInvoiceBuilder(
            siteService.Object,
            new DefaultCheckoutDiscountService([], NullLogger<DefaultCheckoutDiscountService>.Instance),
            new NoTaxCheckoutTaxService());
    }

    private sealed class NoTaxCheckoutTaxService : ICheckoutTaxService
    {
        public Task ApplyTaxAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default)
        {
            invoice.GrandTotal = invoice.DueNow;

            return Task.CompletedTask;
        }

        public Task ApplyRecurringTaxAsync(PaymentRecord payment, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
