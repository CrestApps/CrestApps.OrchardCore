using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The agent checkout's totals always add up to the invoice's grand total, whether tax is added to the prices,
/// already in them, or something the lines don't carry is charged; and what an agent sends is tidied before use.
/// </summary>
public sealed class AgentCheckoutServiceTests
{
    [Fact]
    public void BuildTotals_WithExclusiveTax_SplitsTheInvoiceIntoItsParts()
    {
        // Arrange
        var invoice = Invoice(
            taxAmount: 7.6m,
            grandTotal: 102.6m,
            discount: 10m,
            Line(TaxableItemKind.Physical, 50m, 2),
            Line(TaxableItemKind.Shipping, 5m, 1));

        // Act
        var totals = AgentCheckoutService.BuildTotals(invoice);

        // Assert
        Assert.Equal(100m, totals.Subtotal);
        Assert.Equal(10m, totals.Discount);
        Assert.Equal(5m, totals.Fulfillment);
        Assert.Equal(7.6m, totals.Tax);
        Assert.Equal(0m, totals.Fee);
        Assert.Equal(102.6m, totals.Total);
        AssertAddsUp(totals);
    }

    [Fact]
    public void BuildTotals_WithTaxAlreadyInThePrices_TakesTheTaxOutOfTheParts()
    {
        // Arrange
        var invoice = Invoice(
            taxAmount: 20m,
            grandTotal: 120m,
            discount: 0m,
            Line(TaxableItemKind.Physical, 120m, 1));

        // Act
        var totals = AgentCheckoutService.BuildTotals(invoice);

        // Assert
        Assert.Equal(120m, totals.Subtotal);
        Assert.Equal(0m, totals.Tax);
        Assert.Equal(0m, totals.Fee);
        Assert.Equal(120m, totals.Total);
        AssertAddsUp(totals);
    }

    [Fact]
    public void BuildTotals_WithPartOfTheTaxInThePrices_KeepsTheRest()
    {
        // Arrange
        var invoice = Invoice(
            taxAmount: 15m,
            grandTotal: 110m,
            discount: 0m,
            Line(TaxableItemKind.Physical, 100m, 1));

        // Act
        var totals = AgentCheckoutService.BuildTotals(invoice);

        // Assert
        Assert.Equal(10m, totals.Tax);
        AssertAddsUp(totals);
    }

    [Fact]
    public void BuildTotals_WithAChargeTheLinesDoNotCarry_ShowsItAsAFee()
    {
        // Arrange
        var invoice = Invoice(
            taxAmount: 8m,
            grandTotal: 111.5m,
            discount: 0m,
            Line(TaxableItemKind.Physical, 100m, 1));

        // Act
        var totals = AgentCheckoutService.BuildTotals(invoice);

        // Assert
        Assert.Equal(3.5m, totals.Fee);
        Assert.Equal(8m, totals.Tax);
        AssertAddsUp(totals);
    }

    [Fact]
    public void BuildTotals_WithAnOtherChargeLine_CountsItInTheSubtotal()
    {
        // Arrange
        var invoice = Invoice(
            taxAmount: 0m,
            grandTotal: 52m,
            discount: 0m,
            Line(TaxableItemKind.Physical, 50m, 1),
            Line(TaxableItemKind.OtherCharge, 2m, 1));

        // Act
        var totals = AgentCheckoutService.BuildTotals(invoice);

        // Assert
        Assert.Equal(52m, totals.Subtotal);
        Assert.Equal(0m, totals.Fee);
        AssertAddsUp(totals);
    }

    [Fact]
    public void Normalize_DropsEmptyAndZeroQuantityLinesAndTrimsIds()
    {
        // Arrange
        AgentCheckoutLineInput[] lines =
        [
            new() { ItemId = " p1 ", Quantity = 2 },
            new() { ItemId = "p2", Quantity = 0 },
            new() { ItemId = " ", Quantity = 1 },
            null,
        ];

        // Act
        var normalized = AgentCheckoutService.Normalize(lines);

        // Assert
        var line = Assert.Single(normalized);

        Assert.Equal("p1", line.ItemId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void NormalizeCodes_TrimsAndDropsBlankAndRepeatedCodes()
    {
        // Act
        var codes = AgentCheckoutService.NormalizeCodes([" SAVE10 ", "save10", "", null, "FREESHIP"]);

        // Assert
        Assert.Equal(["SAVE10", "FREESHIP"], codes);
    }

    private static void AssertAddsUp(AgentCheckoutTotals totals)
        => Assert.Equal(totals.Total, totals.Subtotal - totals.Discount + totals.Fulfillment + totals.Tax + totals.Fee);

    private static CheckoutInvoice Invoice(decimal taxAmount, decimal grandTotal, decimal discount, params CheckoutLineItem[] lines)
        => new()
        {
            Currency = "USD",
            TaxAmount = taxAmount,
            GrandTotal = grandTotal,
            LineItems = lines,
            Discounts = discount == 0m ? [] : [new DiscountLine { Amount = discount }],
        };

    private static CheckoutLineItem Line(TaxableItemKind kind, decimal unitPrice, int quantity)
        => new()
        {
            ItemId = kind.ToString(),
            TaxableKind = kind,
            UnitPrice = unitPrice,
            Quantity = quantity,
        };
}
