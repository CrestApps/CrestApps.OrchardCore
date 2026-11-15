using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Moq;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// A cart and an invoice with the same lines come to the same tax at every rounding level: both take their rates from the
/// Taxation module's rules and round at the one level the site sets (Settings > Commerce > Tax calculation).
/// </summary>
public sealed class CheckoutInvoiceTaxParityTests
{
    // Each level and the tax two lines of 10.05 at 7.5% come to (decimals: a double does not convert exactly).
    public static TheoryData<TaxRoundingLevel, decimal> Levels => new()
    {
        { TaxRoundingLevel.Rate, 1.51m },
        { TaxRoundingLevel.Line, 1.50m },
        { TaxRoundingLevel.Tax, 1.51m },
        { TaxRoundingLevel.Jurisdiction, 1.51m },
        { TaxRoundingLevel.Transaction, 1.51m },
    };

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task ACartAndAnInvoiceWithTheSameLines_ComeToTheSameTax_AtEveryLevel(TaxRoundingLevel level, decimal expected)
    {
        // Arrange: two lines of 10.05 at 7.5%. Rounded line by line they are 0.75 + 0.75 = 1.50; on the summed 20.10 the
        // tax is 1.51. The site rounds at the level; neither the cart nor the invoice names one.
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        harness.RoundingLevelProvider.Level = level;
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Sales tax",
            TaxName = "Sales tax",
            TaxType = "SALES",
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });

        var cart = new TaxCalculationContext
        {
            Currency = "USD",
            TransactionDateUtc = TaxTestData.TransactionDate,
            Destination = TaxTestData.California(),
            Items =
            [
                new TaxableItem { Id = "line-1", Kind = TaxableItemKind.Physical, Quantity = 1m, UnitPrice = 10.05m, Currency = "USD" },
                new TaxableItem { Id = "line-2", Kind = TaxableItemKind.Physical, Quantity = 1m, UnitPrice = 10.05m, Currency = "USD" },
            ],
        };

        var invoice = new DocumentCalculationContext
        {
            Currency = "USD",
            IssueDate = DateOnly.FromDateTime(TaxTestData.TransactionDate),
            BillTo = new DocumentParty { ShippingAddress = TaxTestData.California() },
            AutomaticTax = true,
            Lines =
            [
                new DocumentLine { Id = "line-1", Kind = DocumentLineKind.Item, Name = "Widget", Quantity = 1m, UnitPrice = 10.05m },
                new DocumentLine { Id = "line-2", Kind = DocumentLineKind.Item, Name = "Widget", Quantity = 1m, UnitPrice = 10.05m },
            ],
        };

        var calculator = new DocumentCalculator(
            [new CrestApps.OrchardCore.Invoicing.Services.TaxationDocumentTaxProvider(harness.TaxService, Mock.Of<IPartyTaxProfileService>(), harness.Clock)],
            new DefaultTaxBreakdownCalculator(),
            harness.RoundingLevelProvider);

        // Act
        var checkout = await harness.TaxService.CalculateAsync(cart, TestContext.Current.CancellationToken);
        var invoiced = await calculator.CalculateAsync(invoice, TestContext.Current.CancellationToken);

        // Assert: the same tax in total and on each line.
        Assert.Equal(expected, checkout.TaxAmount);
        Assert.Equal(checkout.TaxAmount, invoiced.Totals.TaxTotal);
        Assert.Equal(
            checkout.Lines.OrderBy(line => line.ItemId, StringComparer.Ordinal).Select(line => line.TaxAmount),
            invoice.Lines.Select(line => line.TaxAmount));
    }

    [Fact]
    public async Task AnInvoice_RoundsAtTheSitesLevel_UnlessItNamesOne()
    {
        // Arrange: the site rounds each line; one invoice keeps it, the other names per rate.
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        harness.RoundingLevelProvider.Level = TaxRoundingLevel.Line;
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Sales tax",
            TaxName = "Sales tax",
            TaxType = "SALES",
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });

        var calculator = new DocumentCalculator(
            [new CrestApps.OrchardCore.Invoicing.Services.TaxationDocumentTaxProvider(harness.TaxService, Mock.Of<IPartyTaxProfileService>(), harness.Clock)],
            new DefaultTaxBreakdownCalculator(),
            harness.RoundingLevelProvider);

        // Act
        var siteLevel = await calculator.CalculateAsync(Invoice(null), TestContext.Current.CancellationToken);
        var perRate = await calculator.CalculateAsync(Invoice(TaxRoundingLevel.Rate), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1.50m, siteLevel.Totals.TaxTotal);
        Assert.Equal(1.51m, perRate.Totals.TaxTotal);
    }

    private static DocumentCalculationContext Invoice(TaxRoundingLevel? level)
        => new()
        {
            Currency = "USD",
            IssueDate = DateOnly.FromDateTime(TaxTestData.TransactionDate),
            BillTo = new DocumentParty { ShippingAddress = TaxTestData.California() },
            AutomaticTax = true,
            RoundingLevel = level,
            Lines =
            [
                new DocumentLine { Id = "line-1", Kind = DocumentLineKind.Item, Name = "Widget", Quantity = 1m, UnitPrice = 10.05m },
                new DocumentLine { Id = "line-2", Kind = DocumentLineKind.Item, Name = "Widget", Quantity = 1m, UnitPrice = 10.05m },
            ],
        };
}
