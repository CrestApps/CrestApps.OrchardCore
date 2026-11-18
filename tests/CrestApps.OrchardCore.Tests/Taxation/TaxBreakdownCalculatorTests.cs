using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The Taxation module works out a document's tax with EN 16931 arithmetic: tax per tax and rate on the summed net,
/// spread back over the items; compound taxes on the net plus the other taxes; prices that include tax split once per
/// group; each item's own treatment, else the document's, with a row for every category; and cash rounding as an
/// explicit amount. Invoices, estimates, credit notes and recurring plans all use it.
/// </summary>
public sealed class TaxBreakdownCalculatorTests
{
    private readonly DefaultTaxBreakdownCalculator _calculator = new();

    [Fact]
    public void Calculate_ComputesTaxOnTheSummedNet_AndSpreadsItBackLargestRemainderFirst()
    {
        // Arrange: per item 0.33 x 19% would be 0.06 each (0.18); on the summed 0.99 it is 0.19.
        var request = Request("EUR", Item(0.33m, Vat(19m)), Item(0.33m, Vat(19m)), Item(0.33m, Vat(19m)));

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var tax = Assert.Single(result.Rows);
        Assert.Equal((0.99m, 0.19m), (tax.TaxableAmount, tax.TaxAmount));
        Assert.Equal([0.07m, 0.06m, 0.06m], result.Items.Select(item => item.TaxAmount));
    }

    [Fact]
    public void Calculate_WithPricesThatIncludeTax_KeepsTheGrossAndSplitsOutTheTax()
    {
        // Arrange
        var request = Request("USD", Item(120m, Vat(20m)));
        request.PricesIncludeTax = true;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal((100m, 100m, 20m), (result.Items[0].Net, result.Items[0].BaseNet, result.Items[0].TaxAmount));
        Assert.Equal(120m, result.Total);
    }

    [Fact]
    public void Calculate_WithPricesThatIncludeTax_SplitsTheSummedGrossOnce()
    {
        // Arrange: 10.00 + 10.00 at 20% included: the 20.00 splits into 16.67 and 3.33, not 8.33 + 8.33 and 1.67 + 1.67.
        var request = Request("USD", Item(10m, Vat(20m)), Item(10m, Vat(20m)));
        request.PricesIncludeTax = true;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal(16.67m, result.Items.Sum(item => item.Net));
        Assert.Equal(3.33m, result.TaxTotal);
        Assert.Equal(20m, result.Total);
    }

    [Fact]
    public void Calculate_ChargesACompoundTaxOnTheNetPlusTheOtherTaxes()
    {
        // Arrange
        var request = Request("CAD", Item(100m, Vat(5m, "GST"), Vat(10m, "PST", compound: true)));

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal(5m, result.Rows.Single(row => row.Code == "GST").TaxAmount);
        var compound = result.Rows.Single(row => row.Code == "PST");
        Assert.Equal((true, 105m, 10.5m), (compound.IsCompound, compound.TaxableAmount, compound.TaxAmount));
        Assert.Equal(115.5m, result.Total);
    }

    [Theory]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.ReverseCharge)]
    [InlineData(TaxTreatment.OutOfScope)]
    [InlineData(TaxTreatment.ZeroRated)]
    public void Calculate_ForAPartyThatIsNotTaxable_ChargesNoTax_AndKeepsTheReason(TaxTreatment treatment)
    {
        // Arrange
        var request = Request("EUR", Item(100m, Vat(20m)));
        request.Treatment = treatment;
        request.ExemptionReason = "Reverse charge: the customer accounts for the tax.";

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal((treatment, 0m, 0m, 100m), (row.Treatment, row.TaxAmount, row.Rate, row.TaxableAmount));
        Assert.Equal("Reverse charge: the customer accounts for the tax.", row.ExemptionReason);
        Assert.Equal((100m, 0m), (result.Total, result.Items[0].TaxAmount));
    }

    [Fact]
    public void Calculate_TaxesACharge_InTheSameRowAsItemsOfTheSameRate()
    {
        // Arrange: shipping with its own taxes.
        var shipping = Item(10m, Vat(20m));
        shipping.IsCharge = true;
        var request = Request("USD", Item(100m, Vat(20m)), shipping);

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal((110m, 22m), (row.TaxableAmount, row.TaxAmount));
        Assert.Equal((10m, 2m), (result.Items[1].Net, result.Items[1].TaxAmount));
        Assert.Equal(132m, result.Total);
    }

    // Decimals, not doubles: a double such as 10.02 does not convert to exactly 10.02m.
    public static TheoryData<decimal, decimal, decimal> CashRoundings => new()
    {
        { 10.02m, -0.02m, 10.00m },
        { 10.03m, 0.02m, 10.05m },
        { 10.05m, 0m, 10.05m },
    };

    public static TheoryData<TaxRoundingLevel, decimal, decimal> Levels => new()
    {
        { TaxRoundingLevel.Rate, 1.51m, 0.76m },
        { TaxRoundingLevel.Line, 1.50m, 0.75m },
        { TaxRoundingLevel.Transaction, 1.51m, 0.75m },
    };

    [Theory]
    [MemberData(nameof(CashRoundings))]
    public void Calculate_AddsCashRoundingAsAnExplicitAmount(decimal price, decimal rounding, decimal total)
    {
        // Arrange
        var request = Request("CHF", Item(price));
        request.CashRoundingIncrement = 0.05m;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal((rounding, total), (result.RoundingAmount, result.Total));
    }

    [Fact]
    public void Calculate_ForAZeroDecimalCurrency_RoundsTheTaxToWholeUnits()
    {
        // Arrange: 1,000 yen at 10.5% is 105 yen, never 105.0 with a fraction.
        var request = Request("JPY", Item(1000m, Vat(10.55m)));

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal((106m, 1106m), (result.TaxTotal, result.Total));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calculate_MixingATaxableAndAnExemptItem_GivesEachItsOwnBreakdownRow(bool pricesIncludeTax)
    {
        // Arrange: EN 16931 needs a category per item and a row per category and rate, with the reason on exempt rows.
        var exempt = Item(50m, Vat(20m));
        exempt.Treatment = TaxTreatment.Exempt;
        exempt.TreatmentReason = "Medical supplies";
        var request = Request("EUR", Item(pricesIncludeTax ? 120m : 100m, Vat(20m)), exempt);
        request.PricesIncludeTax = pricesIncludeTax;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var vat = result.Rows.Single(row => row.Treatment == TaxTreatment.Taxable);
        Assert.Equal((100m, 20m), (vat.TaxableAmount, vat.TaxAmount));
        var exemptRow = result.Rows.Single(row => row.Treatment == TaxTreatment.Exempt);
        Assert.Equal((50m, 0m, "Medical supplies"), (exemptRow.TaxableAmount, exemptRow.TaxAmount, exemptRow.ExemptionReason));
        Assert.Equal((0m, 170m), (result.Items[1].TaxAmount, result.Total));
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_AZeroRateTheRulesFoundForAnItem_IsItsOwnRow()
    {
        // Arrange
        var export = Item(80m, new TaxBreakdownRate { Code = "VAT", Name = "VAT", Rate = 0m, Treatment = TaxTreatment.ZeroRated, TreatmentReason = "Export outside the union" });
        var request = Request("EUR", Item(100m, Vat(20m)), export);

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var zero = result.Rows.Single(row => row.Treatment == TaxTreatment.ZeroRated);
        Assert.Equal((80m, 0m, "Export outside the union"), (zero.TaxableAmount, zero.TaxAmount, zero.ExemptionReason));
        Assert.Equal((20m, 200m), (result.TaxTotal, result.Total));
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_ADocumentLevelReverseCharge_KeepsAnItemThatSetsItsOwnTreatment()
    {
        // Arrange: the customer accounts for the tax, but one item is exempt in any case.
        var exempt = Item(30m, Vat(20m));
        exempt.Treatment = TaxTreatment.Exempt;
        exempt.TreatmentReason = "Exempt service";
        var request = Request("EUR", Item(100m, Vat(20m)), exempt);
        request.Treatment = TaxTreatment.ReverseCharge;
        request.ExemptionReason = "Reverse charge";

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var reverse = result.Rows.Single(row => row.Treatment == TaxTreatment.ReverseCharge);
        Assert.Equal((100m, "Reverse charge"), (reverse.TaxableAmount, reverse.ExemptionReason));
        Assert.Equal(30m, result.Rows.Single(row => row.Treatment == TaxTreatment.Exempt).TaxableAmount);
        Assert.Equal((0m, 130m), (result.TaxTotal, result.Total));
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_ATaxableItemWithNoTax_IsInAZeroRow_SoEveryItemHasACategory()
    {
        // Arrange
        var request = Request("USD", Item(100m, Vat(10m)), Item(40m));

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var untaxed = result.Rows.Single(row => row.Rate == 0m);
        Assert.Equal((TaxTreatment.Taxable, 40m), (untaxed.Treatment, untaxed.TaxableAmount));
        AssertRowsAddUp(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calculate_ANegativeItemInTheSameTaxGroup_KeepsTheItemTaxesAddingUpToTheRow(bool pricesIncludeTax)
    {
        // Arrange: a deposit already paid is deducted with a negative line that carries the same tax.
        var request = Request("EUR", Item(pricesIncludeTax ? 120m : 100m, Vat(20m)), Item(pricesIncludeTax ? -24m : -20m, Vat(20m)));
        request.PricesIncludeTax = pricesIncludeTax;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal((80m, 16m), (row.TaxableAmount, row.TaxAmount));
        Assert.Equal(16m, result.Items.Sum(item => item.TaxAmount));
        Assert.Equal(-4m, result.Items[1].TaxAmount);
        Assert.Equal(96m, result.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_UnevenNegativeItems_StillSpreadTheTaxExactly()
    {
        // Arrange
        var request = Request("EUR", Item(33.33m, Vat(19m)), Item(33.33m, Vat(19m)), Item(-10m, Vat(19m)));

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal((56.66m, 10.77m), (row.TaxableAmount, row.TaxAmount));
        Assert.Equal(10.77m, result.Items.Sum(item => item.TaxAmount));
        Assert.True(result.Items[2].TaxAmount < 0m);
    }

    [Theory]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.ReverseCharge)]
    public void Calculate_WithPricesThatIncludeTax_ForAPartyThatIsNotTaxable_KeepsTheGrossAsTheNet(TaxTreatment treatment)
    {
        // Arrange: a price that includes tax is what the customer pays; with no tax to take out, all of it is net.
        var request = Request("EUR", Item(120m, Vat(20m)));
        request.PricesIncludeTax = true;
        request.Treatment = treatment;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal((120m, 0m, 120m), (result.Items[0].Net, result.TaxTotal, result.Total));
    }

    [Fact]
    public void Calculate_ReportsTheNetBeforeTheDocumentDiscounts_ForALineButNotForACharge()
    {
        // Arrange: a line discounted from 100 to 90, beside shipping of 10, prices including 20%.
        var line = Item(108m, Vat(20m));
        line.BaseAmount = 120m;
        var shipping = Item(12m, Vat(20m));
        shipping.IsCharge = true;
        var request = Request("EUR", line, shipping);
        request.PricesIncludeTax = true;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal((90m, 100m), (result.Items[0].Net, result.Items[0].BaseNet));
        Assert.Equal((10m, 10m), (result.Items[1].Net, result.Items[1].BaseNet));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Calculate_RoundsAtTheRequestsLevel(TaxRoundingLevel level, decimal tax, decimal firstItemTax)
    {
        // Arrange: two items of 10.05 at 7.5%: 0.75375 each.
        var request = Request("USD", Item(10.05m, Vat(7.5m)), Item(10.05m, Vat(7.5m)));
        request.RoundingLevel = level;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var row = Assert.Single(result.Rows);
        Assert.Equal((20.10m, tax), (row.TaxableAmount, row.TaxAmount));
        Assert.Equal(firstItemTax, result.Items[0].TaxAmount);
        Assert.Equal(tax, result.Items.Sum(item => item.TaxAmount));
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_AtLineLevel_WithPricesThatIncludeTax_TakesEachLinesTaxOutOfIt()
    {
        // Arrange: 10.75 and 10.75 including 7.5%: each holds 0.75 of tax on a net of 10.00.
        var request = Request("USD", Item(10.75m, Vat(7.5m)), Item(10.75m, Vat(7.5m)));
        request.PricesIncludeTax = true;
        request.RoundingLevel = TaxRoundingLevel.Line;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.All(result.Items, item => Assert.Equal((10m, 10m, 0.75m), (item.Net, item.BaseNet, item.TaxAmount)));
        Assert.Equal(21.50m, result.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_AtLineLevel_ChargesACompoundTaxOnTheNetPlusTheRoundedOtherTaxes()
    {
        // Arrange
        var request = Request("CAD", Item(100m, Vat(5m, "GST"), Vat(10m, "PST", compound: true)));
        request.RoundingLevel = TaxRoundingLevel.Line;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        var compound = result.Rows.Single(row => row.Code == "PST");
        Assert.Equal((105m, 10.5m), (compound.TaxableAmount, compound.TaxAmount));
        Assert.Equal(115.5m, result.Total);
        AssertRowsAddUp(result);
    }

    [Fact]
    public void Calculate_AtJurisdictionLevel_RoundsWhatEachJurisdictionLevies()
    {
        // Arrange: a state and a county tax of 8.25% on 10.10 (0.83325 each), one jurisdiction each, and a second state
        // line. Per jurisdiction: the state's 1.6665 rounds to 1.67, the county's 0.83325 to 0.83.
        var state = Vat(8.25m, "STATE");
        state.JurisdictionId = "ca";
        var county = Vat(8.25m, "COUNTY");
        county.JurisdictionId = "la";
        var request = Request("USD", Item(10.10m, state, county), Item(10.10m, state));
        request.RoundingLevel = TaxRoundingLevel.Jurisdiction;

        // Act
        var result = _calculator.Calculate(request);

        // Assert
        Assert.Equal(1.67m, result.Rows.Single(row => row.Code == "STATE").TaxAmount);
        Assert.Equal(0.83m, result.Rows.Single(row => row.Code == "COUNTY").TaxAmount);
        Assert.Equal(2.50m, result.TaxTotal);
    }

    // Every item is in exactly one non-compound row, so those rows add up to the net of the document, and all rows add up
    // to its tax.
    private static void AssertRowsAddUp(TaxBreakdownResult result)
    {
        Assert.Equal(result.TaxTotal, result.Rows.Sum(row => row.TaxAmount));
        Assert.Equal(result.Items.Sum(item => item.Net), result.Rows.Where(row => !row.IsCompound).Sum(row => row.TaxableAmount));
        Assert.Equal(result.Total, result.Items.Sum(item => item.Net) + result.TaxTotal + result.RoundingAmount);
    }

    private static TaxBreakdownRequest Request(string currency, params TaxBreakdownItem[] items)
    {
        var request = new TaxBreakdownRequest { Currency = currency };

        foreach (var item in items)
        {
            request.Items.Add(item);
        }

        return request;
    }

    private static TaxBreakdownItem Item(decimal amount, params TaxBreakdownRate[] taxes)
    {
        var item = new TaxBreakdownItem { Id = Guid.NewGuid().ToString("n"), Amount = amount, BaseAmount = amount };

        foreach (var tax in taxes)
        {
            item.Taxes.Add(tax);
        }

        return item;
    }

    private static TaxBreakdownRate Vat(decimal rate, string code = "VAT", bool compound = false)
        => new() { Treatment = TaxTreatment.Taxable, Code = code, Name = code, Rate = rate, IsCompound = compound };
}
