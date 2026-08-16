using System.Threading.Tasks;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Verifies that subscription checkout consumes the taxation framework as the single source of truth:
/// tax is added to the invoice, detailed tax lines and an immutable snapshot are captured, taxation is
/// optional, and recurring billing is redetermined per cycle.
/// </summary>
public sealed class SubscriptionTaxIntegrationTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    private static SubscriptionFlow CreateFlow()
        => new(new SubscriptionSession(), new ContentItem());

    private static SubscriptionTaxService CreateService(TaxTestHarness harness, SubscriptionTaxProfile profile)
        => new(
            harness.TaxService,
            harness.GetService<ITaxSnapshotFactory>(),
            new FixedSubscriptionTaxProfileProvider(profile),
            harness.Clock);

    private static async Task<string> SeedCaliforniaPercentageRuleAsync(TaxTestHarness harness, decimal rate, string code = "US-CA-SALES")
    {
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = code,
            TaxType = TaxTypeNames.SalesTax,
            TaxName = code,
            TaxCode = code,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = rate,
        });

        return jurisdictionId;
    }

    private static Invoice CreateInvoice(double dueNow, params InvoiceLineItem[] lineItems)
        => new()
        {
            Currency = "USD",
            DueNow = dueNow,
            InitialPaymentAmount = dueNow,
            LineItems = lineItems,
        };

    [Fact]
    public async Task ApplyTax_WithExclusivePercentage_AddsTaxLinesSnapshotAndGrandTotal()
    {
        var harness = CreateHarness();
        await SeedCaliforniaPercentageRuleAsync(harness, 0.075m);

        var invoice = CreateInvoice(200d, new InvoiceLineItem
        {
            Id = "line-1",
            Description = "Setup fee",
            Quantity = 1,
            UnitPrice = 200d,
        });

        var service = CreateService(harness, new SubscriptionTaxProfile
        {
            Destination = TaxTestData.California(),
        });

        await service.ApplyTaxAsync(invoice, CreateFlow(), TestContext.Current.CancellationToken);

        Assert.Equal(15d, invoice.TaxAmount);
        Assert.Equal(215d, invoice.GrandTotal);
        // The exclusive tax must be folded into the amount the up-front PaymentIntent actually charges.
        Assert.Equal(215d, invoice.InitialPaymentAmount);
        Assert.NotNull(invoice.TaxSnapshot);
        Assert.Equal(15m, invoice.TaxSnapshot.TaxAmount);
        var line = Assert.Single(invoice.TaxLines);
        Assert.Equal(15m, line.TaxAmount);
        Assert.Equal("California", line.JurisdictionName);
    }

    [Fact]
    public async Task ApplyTax_WhenTaxationDisabled_DoesNotChangeInitialPaymentAmount()
    {
        var invoice = CreateInvoice(200d, new InvoiceLineItem
        {
            Id = "line-1",
            Quantity = 1,
            UnitPrice = 200d,
        });

        var service = new NullSubscriptionTaxService();

        await service.ApplyTaxAsync(invoice, CreateFlow(), TestContext.Current.CancellationToken);

        Assert.Equal(200d, invoice.InitialPaymentAmount);
    }

    [Fact]
    public async Task ApplyTax_WhenTaxationDisabled_LeavesGrandTotalEqualToDueNow()
    {
        var invoice = CreateInvoice(200d, new InvoiceLineItem
        {
            Id = "line-1",
            Quantity = 1,
            UnitPrice = 200d,
        });

        var service = new NullSubscriptionTaxService();

        await service.ApplyTaxAsync(invoice, CreateFlow(), TestContext.Current.CancellationToken);

        Assert.Equal(0d, invoice.TaxAmount);
        Assert.Equal(200d, invoice.GrandTotal);
        Assert.Null(invoice.TaxSnapshot);
        Assert.Null(invoice.TaxLines);
    }

    [Fact]
    public async Task ApplyTax_InclusivePricing_DoesNotAddTaxOnTopButRecordsTax()
    {
        var harness = CreateHarness();
        await SeedCaliforniaPercentageRuleAsync(harness, 0.075m);

        var invoice = CreateInvoice(215d, new InvoiceLineItem
        {
            Id = "line-1",
            Quantity = 1,
            UnitPrice = 215d,
        });

        var service = CreateService(harness, new SubscriptionTaxProfile
        {
            Destination = TaxTestData.California(),
            PriceType = TaxPriceType.Inclusive,
        });

        await service.ApplyTaxAsync(invoice, CreateFlow(), TestContext.Current.CancellationToken);

        // Tax is extracted from the inclusive price, so the grand total remains the amount due now.
        Assert.Equal(215d, invoice.GrandTotal);
        Assert.True(invoice.TaxAmount > 0d);
        Assert.NotNull(invoice.TaxSnapshot);
    }

    [Fact]
    public async Task ApplyTax_MultipleTaxes_PreservesEachTaxLine()
    {
        var harness = CreateHarness();
        var jurisdictionId = await SeedCaliforniaPercentageRuleAsync(harness, 0.06m, "US-CA-STATE");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "US-CA-DISTRICT",
            TaxType = "DistrictTax",
            TaxName = "US-CA-DISTRICT",
            TaxCode = "US-CA-DISTRICT",
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.01m,
        });

        var invoice = CreateInvoice(100d, new InvoiceLineItem
        {
            Id = "line-1",
            Quantity = 1,
            UnitPrice = 100d,
        });

        var service = CreateService(harness, new SubscriptionTaxProfile
        {
            Destination = TaxTestData.California(),
        });

        await service.ApplyTaxAsync(invoice, CreateFlow(), TestContext.Current.CancellationToken);

        Assert.Equal(2, invoice.TaxLines.Count);
        Assert.Equal(7d, invoice.TaxAmount);
        Assert.Equal(107d, invoice.GrandTotal);
    }

    [Fact]
    public void ContextFactory_ExcludesDelayedSubscriptionLines()
    {
        var invoice = new Invoice
        {
            Currency = "USD",
            LineItems =
            [
                new InvoiceLineItem { Id = "initial", Quantity = 1, UnitPrice = 50d },
                new InvoiceLineItem
                {
                    Id = "now",
                    Quantity = 1,
                    UnitPrice = 20d,
                    Subscription = new SubscriptionPlan { SubscriptionDayDelay = 0 },
                },
                new InvoiceLineItem
                {
                    Id = "delayed",
                    Quantity = 1,
                    UnitPrice = 30d,
                    Subscription = new SubscriptionPlan { SubscriptionDayDelay = 30 },
                },
            ],
        };

        var context = SubscriptionTaxContextFactory.Create(invoice, new SubscriptionTaxProfile(), TaxTestData.TransactionDate);

        Assert.Equal(2, context.Items.Count);
        Assert.DoesNotContain(context.Items, item => item.Id == "delayed");
    }

    [Fact]
    public async Task RecurringCharge_UsesCurrentRules_WhileHistoricalSnapshotIsPreserved()
    {
        var harness = CreateHarness();
        await SeedCaliforniaPercentageRuleAsync(harness, 0.08m);
        var snapshotFactory = harness.GetService<ITaxSnapshotFactory>();

        var lineItem = new InvoiceLineItem
        {
            Id = "cycle",
            Quantity = 1,
            UnitPrice = 100d,
            Subscription = new SubscriptionPlan(),
        };

        var profile = new SubscriptionTaxProfile { Destination = TaxTestData.California() };

        // Billing #1 at 8%.
        var firstContext = SubscriptionTaxContextFactory.CreateForRecurringCharge(lineItem, "USD", profile, TaxTestData.TransactionDate);
        var firstResult = await harness.TaxService.CalculateAsync(firstContext, TestContext.Current.CancellationToken);
        var firstSnapshot = snapshotFactory.Create(firstContext, firstResult);

        Assert.Equal(8m, firstResult.TaxAmount);

        // The tax rate changes to 9% before the next billing cycle.
        var rule = Assert.Single(await harness.Rules.GetAllAsync(TestContext.Current.CancellationToken));
        rule.Rate = 0.09m;
        await harness.Rules.UpdateAsync(rule, TestContext.Current.CancellationToken);

        // Billing #2 uses the current 9% rule.
        var secondContext = SubscriptionTaxContextFactory.CreateForRecurringCharge(lineItem, "USD", profile, TaxTestData.TransactionDate);
        var secondResult = await harness.TaxService.CalculateAsync(secondContext, TestContext.Current.CancellationToken);

        Assert.Equal(9m, secondResult.TaxAmount);

        // Billing #1 retains its original 8% snapshot.
        Assert.Equal(8m, firstSnapshot.TaxAmount);
    }

    [Fact]
    public async Task ApplyRecurringTax_FromSession_RedeterminesTaxAndSnapshotsPerCycle()
    {
        var harness = CreateHarness();
        await SeedCaliforniaPercentageRuleAsync(harness, 0.08m);

        var session = new SubscriptionSession();
        var service = CreateService(harness, new SubscriptionTaxProfile { Destination = TaxTestData.California() });

        // The provider-charged amount is authoritative and treated as tax-inclusive: at 8% the $108
        // charge yields $8 of embedded tax on a $100 net.
        var payment = new PaymentInfo { Currency = "USD", Amount = 108d };
        await service.ApplyRecurringTaxAsync(payment, session, TestContext.Current.CancellationToken);

        Assert.Equal(8d, payment.TaxAmount);
        Assert.NotNull(payment.TaxSnapshot);
        Assert.Equal(8m, payment.TaxSnapshot.TaxAmount);

        // A later cycle after the rate changes gets a fresh snapshot; the earlier one is untouched.
        var rule = Assert.Single(await harness.Rules.GetAllAsync(TestContext.Current.CancellationToken));
        rule.Rate = 0.09m;
        await harness.Rules.UpdateAsync(rule, TestContext.Current.CancellationToken);

        var firstSnapshot = payment.TaxSnapshot;
        var secondPayment = new PaymentInfo { Currency = "USD", Amount = 109d };
        await service.ApplyRecurringTaxAsync(secondPayment, session, TestContext.Current.CancellationToken);

        Assert.Equal(9d, secondPayment.TaxAmount);
        Assert.Equal(8m, firstSnapshot.TaxAmount);
    }

    [Fact]
    public async Task DefaultProfileProvider_Session_ReadsClassificationFromInvoiceAndDestinationFromCard()
    {
        var session = new SubscriptionSession();
        session.Put(new Invoice
        {
            Currency = "USD",
            TaxCategoryCode = "DIGITAL",
            TaxClassificationCode = "SAAS",
        });
        session.Put(new SubscriptionInfo
        {
            PaymentMethod = new PaymentMethodInfo
            {
                Card = new PaymentCardInfo { Country = "US" },
            },
        });

        var profile = await new DefaultSubscriptionTaxProfileProvider()
            .GetProfileAsync(session, TestContext.Current.CancellationToken);

        Assert.Equal("DIGITAL", profile.DefaultTaxCategoryCode);
        Assert.Equal("SAAS", profile.DefaultTaxClassificationCode);
        Assert.Equal("US", profile.Destination?.Country);
    }

    [Fact]
    public async Task ApplyRecurringTax_WhenTaxationDisabled_RecordsNoTax()
    {
        var session = new SubscriptionSession();
        var payment = new PaymentInfo { Currency = "USD", Amount = 100d };

        var service = new NullSubscriptionTaxService();

        await service.ApplyRecurringTaxAsync(payment, session, TestContext.Current.CancellationToken);

        Assert.Equal(0d, payment.TaxAmount);
        Assert.Null(payment.TaxSnapshot);
    }
}
