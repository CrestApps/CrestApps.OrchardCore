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
/// A cart and an invoice with the same lines come to the same tax: both take their rates from the Taxation module's
/// rules and round each tax and rate once on the summed amount, as EN 16931 asks.
/// </summary>
public sealed class CheckoutInvoiceTaxParityTests
{
    [Fact]
    public async Task ACartAndAnInvoiceWithTheSameLines_ComeToTheSameTax()
    {
        // Arrange: two lines of 10.05 at 7.5%. Rounded line by line they would be 0.75 + 0.75 = 1.50; on the summed 20.10
        // the tax is 1.51.
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
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
            new DefaultTaxBreakdownCalculator());

        // Act
        var checkout = await harness.TaxService.CalculateAsync(cart, TestContext.Current.CancellationToken);
        var invoiced = await calculator.CalculateAsync(invoice, TestContext.Current.CancellationToken);

        // Assert: the same tax in total and on each line.
        Assert.Equal(1.51m, checkout.TaxAmount);
        Assert.Equal(checkout.TaxAmount, invoiced.Totals.TaxTotal);
        Assert.Equal(
            checkout.Lines.OrderBy(line => line.ItemId, StringComparer.Ordinal).Select(line => line.TaxAmount),
            invoice.Lines.Select(line => line.TaxAmount));
    }
}
