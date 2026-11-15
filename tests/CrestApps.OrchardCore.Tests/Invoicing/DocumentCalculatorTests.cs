using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// One calculator computes every invoicing document: line nets rounded to the currency and document discounts spread in
/// proportion; the tax is the Taxation module's breakdown (its cases are in TaxBreakdownCalculatorTests), written back to
/// the lines and charges.
/// </summary>
public sealed class DocumentCalculatorTests
{
    private readonly DocumentCalculator _calculator = new([], new DefaultTaxBreakdownCalculator());

    [Fact]
    public void Calculate_RoundsEachLineNetToTheCurrency()
    {
        // Arrange
        var context = Context("USD", Line(3m, 3.333m, Vat(20m)), Line(2m, 10m, Vat(20m)));

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(10.00m, context.Lines[0].Net);
        Assert.Equal(20.00m, context.Lines[1].Net);
        Assert.Equal(30.00m, result.Totals.Subtotal);
        Assert.Equal(6.00m, result.Totals.TaxTotal);
        Assert.Equal(36.00m, result.Totals.Total);
    }

    [Fact]
    public void Calculate_WritesTheTaxationBreakdownBackToTheLines()
    {
        // Arrange: per line 0.33 x 19% would be 0.06 each (0.18); on the summed 0.99 it is 0.19.
        var context = Context("EUR", Line(1m, 0.33m, Vat(19m)), Line(1m, 0.33m, Vat(19m)), Line(1m, 0.33m, Vat(19m)));

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var tax = Assert.Single(result.Taxes);
        Assert.Equal(0.99m, tax.TaxableAmount);
        Assert.Equal(0.19m, tax.TaxAmount);
        Assert.Equal(0.19m, context.Lines.Sum(line => line.TaxAmount));
        Assert.Equal([0.07m, 0.06m, 0.06m], context.Lines.Select(line => line.TaxAmount));
    }

    [Fact]
    public void Calculate_TakesTheLineDiscount_ByPercentOrByAmount()
    {
        // Arrange
        var byPercent = Line(4m, 25m, Vat(10m));
        byPercent.DiscountPercent = 10m;
        var byAmount = Line(1m, 50m, Vat(10m));
        byAmount.DiscountAmount = 7.5m;
        byAmount.DiscountPercent = 90m;

        var context = Context("USD", byPercent, byAmount);

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(90m, byPercent.Net);
        Assert.Equal(42.5m, byAmount.Net);
        Assert.Equal(132.5m, result.Totals.Subtotal);
        Assert.Equal(13.25m, result.Totals.TaxTotal);
    }

    [Fact]
    public void Calculate_SpreadsADocumentDiscountOverTheLines_SoEachRateIsTaxedOnItsShare()
    {
        // Arrange: 10% off 100 at 20% and 50 at 10%.
        var context = Context("USD", Line(1m, 100m, Vat(20m)), Line(1m, 50m, Vat(10m)));
        context.Charges.Add(new DocumentCharge { Kind = DocumentChargeKind.Discount, Name = "Loyalty", Percent = 10m });

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(10m, context.Lines[0].DocumentDiscount);
        Assert.Equal(5m, context.Lines[1].DocumentDiscount);
        Assert.Equal(15m, context.Charges[0].Net);
        Assert.Equal(15m, result.Totals.DiscountTotal);
        Assert.Equal(18m, result.Taxes.Single(tax => tax.Rate == 20m).TaxAmount);
        Assert.Equal(90m, result.Taxes.Single(tax => tax.Rate == 20m).TaxableAmount);
        Assert.Equal(4.5m, result.Taxes.Single(tax => tax.Rate == 10m).TaxAmount);
        Assert.Equal(157.5m, result.Totals.Total);
        Assert.Equal(108m, context.Lines[0].Total);
    }

    [Fact]
    public void Calculate_SpreadsAnUnevenDiscountAmount_LargestRemainderFirst()
    {
        // Arrange
        var context = Context("USD", Line(1m, 10m), Line(1m, 10m), Line(1m, 10m));
        context.Charges.Add(new DocumentCharge { Kind = DocumentChargeKind.Discount, Amount = 10m });

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal([3.34m, 3.33m, 3.33m], context.Lines.Select(line => line.DocumentDiscount));
        Assert.Equal(10m, result.Totals.DiscountTotal);
        Assert.Equal(20m, result.Totals.Total);
    }

    [Fact]
    public void Calculate_NeverSpreadsADiscountOverANegativeLine()
    {
        // Arrange: a deposit already paid is deducted with a negative line, which takes no discount.
        var deposit = Line(1m, -40m);
        deposit.Kind = DocumentLineKind.Deposit;
        var context = Context("USD", Line(1m, 100m), deposit);
        context.Charges.Add(new DocumentCharge { Kind = DocumentChargeKind.Discount, Percent = 10m });

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(10m, context.Lines[0].DocumentDiscount);
        Assert.Equal(0m, deposit.DocumentDiscount);
        Assert.Equal(50m, result.Totals.Total);
    }

    [Fact]
    public void Calculate_TaxesShippingWithItsOwnTaxes_AndWritesItBackToTheCharge()
    {
        // Arrange
        var context = Context("USD", Line(1m, 100m, Vat(20m)));
        var shipping = new DocumentCharge { Kind = DocumentChargeKind.Shipping, Name = "Freight", Amount = 10m };
        shipping.Taxes.Add(Vat(20m));
        context.Charges.Add(shipping);

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var row = Assert.Single(result.Taxes);
        Assert.Equal(110m, row.TaxableAmount);
        Assert.Equal(22m, row.TaxAmount);
        Assert.Equal(10m, result.Totals.ChargeTotal);
        Assert.Equal(2m, shipping.TaxAmount);
        Assert.Equal(132m, result.Totals.Total);
    }

    [Fact]
    public void Calculate_ForAZeroDecimalCurrency_RoundsToWholeUnits()
    {
        // Arrange
        var context = Context("JPY", Line(3m, 333.3m, Vat(10m)));

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(1000m, context.Lines[0].Net);
        Assert.Equal(100m, result.Totals.TaxTotal);
        Assert.Equal(1100m, result.Totals.Total);
    }

    [Fact]
    public void Calculate_IgnoresTextAndSectionLines()
    {
        // Arrange
        var section = new DocumentLine { Kind = DocumentLineKind.Section, Name = "Labor", Quantity = 5m, UnitPrice = 100m };
        var text = new DocumentLine { Kind = DocumentLineKind.Text, Name = "Thank you", Quantity = 1m, UnitPrice = 9m };
        var context = Context("USD", Line(1m, 50m));
        context.Lines.Insert(0, section);
        context.Lines.Add(text);

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(0m, section.Net);
        Assert.Equal(0m, text.Total);
        Assert.Equal(50m, result.Totals.Total);
    }

    [Theory]
    [InlineData(150, null, 0)]
    [InlineData(-5, null, 100)]
    [InlineData(null, 120, 0)]
    [InlineData(null, -10, 100)]
    public void Calculate_ClampsALineDiscountToTheLineAmount(int? amount, int? percent, decimal expectedNet)
    {
        // Arrange: validation refuses such a discount; a preview still never shows a negative or grown line.
        var line = Line(1m, 100m);
        line.DiscountAmount = amount;
        line.DiscountPercent = percent;

        // Act
        var result = _calculator.Calculate(Context("USD", line));

        // Assert
        Assert.Equal(expectedNet, line.Net);
        Assert.Equal(expectedNet, result.Totals.Total);
    }

    [Fact]
    public async Task CalculateAsync_LetsTheFirstTaxProviderThatHandlesItSetTheTaxes()
    {
        // Arrange
        var calculator = new DocumentCalculator([new FixedRateProvider(order: 20, rate: 99m), new FixedRateProvider(order: 10, rate: 8m)], new DefaultTaxBreakdownCalculator());
        var context = Context("USD", Line(1m, 100m));
        context.AutomaticTax = true;

        // Act
        var result = await calculator.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(8m, result.Totals.TaxTotal);
    }

    [Fact]
    public async Task CalculateAsync_WithoutAutomaticTax_KeepsTheTypedTaxes()
    {
        // Arrange
        var calculator = new DocumentCalculator([new FixedRateProvider(order: 10, rate: 8m)], new DefaultTaxBreakdownCalculator());
        var context = Context("USD", Line(1m, 100m, Vat(20m)));

        // Act
        var result = await calculator.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20m, result.Totals.TaxTotal);
    }

    // Every line and charge is in exactly one non-compound row, so those rows add up to the net of the document, and all
    // rows add up to its tax.
    private static void AssertRowsAddUp(DocumentCalculationResult result)
    {
        var totals = result.Totals;

        Assert.Equal(totals.TaxTotal, result.Taxes.Sum(row => row.TaxAmount));
        Assert.Equal(totals.Subtotal - totals.DiscountTotal + totals.ChargeTotal, result.Taxes.Where(row => !row.IsCompound).Sum(row => row.TaxableAmount));
        Assert.Equal(totals.Total, totals.Subtotal - totals.DiscountTotal + totals.ChargeTotal + totals.TaxTotal + totals.RoundingAmount);
    }

    private static DocumentCalculationContext Context(string currency, params DocumentLine[] lines)
        => new()
        {
            Currency = currency,
            Lines = [.. lines],
        };

    private static DocumentLine Line(decimal quantity, decimal unitPrice, params DocumentLineTax[] taxes)
        => new()
        {
            Id = Guid.NewGuid().ToString("n"),
            Kind = DocumentLineKind.Item,
            Name = "Item",
            Quantity = quantity,
            UnitPrice = unitPrice,
            Taxes = [.. taxes],
        };

    private static DocumentLineTax Vat(decimal rate, string code = "VAT", bool compound = false)
        => new()
        {
            Code = code,
            Name = code,
            Rate = rate,
            IsCompound = compound,
        };

    private sealed class FixedRateProvider : IDocumentTaxProvider
    {
        private readonly decimal _rate;

        public FixedRateProvider(int order, decimal rate)
        {
            Order = order;
            _rate = rate;
        }

        public int Order { get; }

        public Task<bool> ApplyTaxesAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default)
        {
            foreach (var line in context.Lines)
            {
                line.Taxes = [Vat(_rate, "Sales")];
            }

            return Task.FromResult(true);
        }
    }
}
