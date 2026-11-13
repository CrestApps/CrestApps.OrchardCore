using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// One calculator computes every invoicing document with EN 16931 arithmetic: line nets rounded to the currency, tax per
/// tax and rate on the summed net and spread back over the lines, document discounts spread in proportion, inclusive
/// prices split into net and tax, and cash rounding as an explicit amount.
/// </summary>
public sealed class DocumentCalculatorTests
{
    private readonly DocumentCalculator _calculator = new([]);

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
    public void Calculate_ComputesTaxOnTheSummedNet_AndSpreadsItBackLargestRemainderFirst()
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
    public void Calculate_WithPricesThatIncludeTax_KeepsTheGrossAndSplitsOutTheTax()
    {
        // Arrange
        var context = Context("USD", Line(1m, 120m, Vat(20m)));
        context.PricesIncludeTax = true;

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(100m, context.Lines[0].Net);
        Assert.Equal(20m, context.Lines[0].TaxAmount);
        Assert.Equal(120m, result.Totals.Total);
    }

    [Fact]
    public void Calculate_WithPricesThatIncludeTax_SplitsTheSummedGrossOnce()
    {
        // Arrange: 10.00 + 10.00 at 20% included: the 20.00 splits into 16.67 and 3.33, not 8.33 + 8.33 and 1.67 + 1.67.
        var context = Context("USD", Line(1m, 10m, Vat(20m)), Line(1m, 10m, Vat(20m)));
        context.PricesIncludeTax = true;

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(16.67m, result.Totals.Subtotal);
        Assert.Equal(3.33m, result.Totals.TaxTotal);
        Assert.Equal(20m, result.Totals.Total);
        Assert.Equal(16.67m, context.Lines.Sum(line => line.Net));
        Assert.Equal(20m, context.Lines.Sum(line => line.Total));
    }

    [Fact]
    public void Calculate_ChargesACompoundTaxOnTheNetPlusTheOtherTaxes()
    {
        // Arrange
        var context = Context("CAD", Line(1m, 100m, Vat(5m, "GST"), Vat(10m, "PST", compound: true)));

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(5m, result.Taxes.Single(tax => tax.Code == "GST").TaxAmount);
        var compound = result.Taxes.Single(tax => tax.Code == "PST");
        Assert.True(compound.IsCompound);
        Assert.Equal(105m, compound.TaxableAmount);
        Assert.Equal(10.5m, compound.TaxAmount);
        Assert.Equal(115.5m, result.Totals.Total);
    }

    [Theory]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.ReverseCharge)]
    [InlineData(TaxTreatment.OutOfScope)]
    [InlineData(TaxTreatment.ZeroRated)]
    public void Calculate_ForAPartyThatIsNotTaxable_ChargesNoTax_AndKeepsTheReason(TaxTreatment treatment)
    {
        // Arrange
        var context = Context("EUR", Line(1m, 100m, Vat(20m)));
        context.TaxTreatment = treatment;
        context.ExemptionReason = "Reverse charge: the customer accounts for the tax.";

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var row = Assert.Single(result.Taxes);
        Assert.Equal(treatment, row.Treatment);
        Assert.Equal(0m, row.TaxAmount);
        Assert.Equal(0m, row.Rate);
        Assert.Equal(100m, row.TaxableAmount);
        Assert.Equal("Reverse charge: the customer accounts for the tax.", row.ExemptionReason);
        Assert.Equal(100m, result.Totals.Total);
        Assert.Equal(0m, context.Lines[0].TaxAmount);
    }

    [Fact]
    public void Calculate_TaxesShippingWithItsOwnTaxes_InTheSameRowAsLinesOfTheSameRate()
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

    [Theory]
    [InlineData(10.02, -0.02, 10.00)]
    [InlineData(10.03, 0.02, 10.05)]
    [InlineData(10.05, 0, 10.05)]
    public void Calculate_AddsCashRoundingAsAnExplicitAmount(decimal price, decimal rounding, decimal total)
    {
        // Arrange
        var context = Context("CHF", Line(1m, price));
        context.CashRoundingIncrement = 0.05m;

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(rounding, result.Totals.RoundingAmount);
        Assert.Equal(total, result.Totals.Total);
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
    [InlineData(false)]
    [InlineData(true)]
    public void Calculate_MixingATaxableAndAnExemptLine_GivesEachItsOwnBreakdownRow(bool pricesIncludeTax)
    {
        // Arrange: EN 16931 needs a category per line and a row per category and rate, with the reason on exempt rows.
        var taxed = Line(1m, pricesIncludeTax ? 120m : 100m, Vat(20m));
        var exempt = Line(1m, 50m, Vat(20m));
        exempt.TaxTreatment = TaxTreatment.Exempt;
        exempt.TaxTreatmentReason = "Medical supplies";

        var context = Context("EUR", taxed, exempt);
        context.PricesIncludeTax = pricesIncludeTax;

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var vat = result.Taxes.Single(row => row.Treatment == TaxTreatment.Taxable);
        Assert.Equal(100m, vat.TaxableAmount);
        Assert.Equal(20m, vat.TaxAmount);
        var exemptRow = result.Taxes.Single(row => row.Treatment == TaxTreatment.Exempt);
        Assert.Equal(50m, exemptRow.TaxableAmount);
        Assert.Equal(0m, exemptRow.TaxAmount);
        Assert.Equal("Medical supplies", exemptRow.ExemptionReason);
        Assert.Equal(0m, exempt.TaxAmount);
        Assert.Equal(170m, result.Totals.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_ALineLevelZeroRate_FromATaxProvider_IsItsOwnRow()
    {
        // Arrange
        var export = Line(1m, 80m, new DocumentLineTax { Code = "VAT", Name = "VAT", Rate = 0m, Treatment = TaxTreatment.ZeroRated, TreatmentReason = "Export outside the union" });
        var context = Context("EUR", Line(1m, 100m, Vat(20m)), export);

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var zero = result.Taxes.Single(row => row.Treatment == TaxTreatment.ZeroRated);
        Assert.Equal(80m, zero.TaxableAmount);
        Assert.Equal(0m, zero.TaxAmount);
        Assert.Equal("Export outside the union", zero.ExemptionReason);
        Assert.Equal(20m, result.Totals.TaxTotal);
        Assert.Equal(200m, result.Totals.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_ADocumentLevelReverseCharge_KeepsALineThatSetsItsOwnTreatment()
    {
        // Arrange: the customer accounts for the tax, but one line is exempt in any case.
        var exempt = Line(1m, 30m, Vat(20m));
        exempt.TaxTreatment = TaxTreatment.Exempt;
        exempt.TaxTreatmentReason = "Exempt service";

        var context = Context("EUR", Line(1m, 100m, Vat(20m)), exempt);
        context.TaxTreatment = TaxTreatment.ReverseCharge;
        context.ExemptionReason = "Reverse charge";

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(100m, result.Taxes.Single(row => row.Treatment == TaxTreatment.ReverseCharge).TaxableAmount);
        Assert.Equal("Reverse charge", result.Taxes.Single(row => row.Treatment == TaxTreatment.ReverseCharge).ExemptionReason);
        Assert.Equal(30m, result.Taxes.Single(row => row.Treatment == TaxTreatment.Exempt).TaxableAmount);
        Assert.Equal(0m, result.Totals.TaxTotal);
        Assert.Equal(130m, result.Totals.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_ATaxableLineWithNoTax_IsInAZeroRow_SoEveryLineHasACategory()
    {
        // Arrange
        var context = Context("USD", Line(1m, 100m, Vat(10m)), Line(1m, 40m));

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var untaxed = result.Taxes.Single(row => row.Rate == 0m);
        Assert.Equal(TaxTreatment.Taxable, untaxed.Treatment);
        Assert.Equal(40m, untaxed.TaxableAmount);
        AssertRowsAddUp(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calculate_ANegativeLineInTheSameTaxGroup_KeepsTheLineTaxesAddingUpToTheRow(bool pricesIncludeTax)
    {
        // Arrange: a deposit already paid is deducted with a negative line that carries the same tax.
        var deposit = Line(1m, pricesIncludeTax ? -24m : -20m, Vat(20m));
        deposit.Kind = DocumentLineKind.Deposit;
        var context = Context("EUR", Line(1m, pricesIncludeTax ? 120m : 100m, Vat(20m)), deposit);
        context.PricesIncludeTax = pricesIncludeTax;

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var row = Assert.Single(result.Taxes);
        Assert.Equal(80m, row.TaxableAmount);
        Assert.Equal(16m, row.TaxAmount);
        Assert.Equal(16m, context.Lines.Sum(line => line.TaxAmount));
        Assert.Equal(-4m, deposit.TaxAmount);
        Assert.Equal(96m, result.Totals.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_UnevenNegativeLines_StillSpreadTheTaxExactly()
    {
        // Arrange
        var context = Context("EUR", Line(1m, 33.33m, Vat(19m)), Line(1m, 33.33m, Vat(19m)), Line(1m, -10m, Vat(19m)));

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        var row = Assert.Single(result.Taxes);
        Assert.Equal(56.66m, row.TaxableAmount);
        Assert.Equal(10.77m, row.TaxAmount);
        Assert.Equal(10.77m, context.Lines.Sum(line => line.TaxAmount));
        Assert.True(context.Lines[2].TaxAmount < 0m);
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

    [Theory]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.ReverseCharge)]
    public void Calculate_WithPricesThatIncludeTax_ForAPartyThatIsNotTaxable_KeepsTheGrossAsTheNet(TaxTreatment treatment)
    {
        // Arrange: a price that includes tax is what the customer pays; with no tax to take out, all of it is net.
        var context = Context("EUR", Line(1m, 120m, Vat(20m)));
        context.PricesIncludeTax = true;
        context.TaxTreatment = treatment;

        // Act
        var result = _calculator.Calculate(context);

        // Assert
        Assert.Equal(120m, context.Lines[0].Net);
        Assert.Equal(0m, result.Totals.TaxTotal);
        Assert.Equal(120m, result.Totals.Total);
    }

    [Fact]
    public async Task CalculateAsync_LetsTheFirstTaxProviderThatHandlesItSetTheTaxes()
    {
        // Arrange
        var calculator = new DocumentCalculator([new FixedRateProvider(order: 20, rate: 99m), new FixedRateProvider(order: 10, rate: 8m)]);
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
        var calculator = new DocumentCalculator([new FixedRateProvider(order: 10, rate: 8m)]);
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
