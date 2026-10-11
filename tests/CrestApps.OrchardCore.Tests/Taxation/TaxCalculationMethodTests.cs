using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxCalculationMethodTests
{
    [Fact]
    public void Percentage_Exclusive_ComputesTaxOnTop()
    {
        var method = new PercentageTaxCalculationMethod();

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 100m,
            Rate = 0.10m,
            PriceIncludesTax = false,
        });

        Assert.Equal(100m, result.TaxableAmount);
        Assert.Equal(10m, result.TaxAmount);
        Assert.Equal(0.10m, result.EffectiveRate);
    }

    [Fact]
    public void Percentage_Inclusive_ExtractsEmbeddedTax()
    {
        var method = new PercentageTaxCalculationMethod();

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 110m,
            Rate = 0.10m,
            PriceIncludesTax = true,
        });

        Assert.Equal(100m, result.TaxableAmount);
        Assert.Equal(10m, result.TaxAmount);
    }

    [Fact]
    public void FixedAmount_ReturnsConstantTax()
    {
        var method = new FixedAmountTaxCalculationMethod();

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 500m,
            FixedAmount = 7m,
        });

        Assert.Equal(7m, result.TaxAmount);
        Assert.Equal(0m, result.EffectiveRate);
    }

    [Fact]
    public void PerUnit_MultipliesByQuantity()
    {
        var method = new PerUnitTaxCalculationMethod();

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 200m,
            Quantity = 4m,
            FixedAmount = 2.5m,
        });

        Assert.Equal(10m, result.TaxAmount);
    }

    [Fact]
    public void PerWeight_MultipliesByWeight()
    {
        var method = new PerWeightTaxCalculationMethod();

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 200m,
            Weight = 3m,
            FixedAmount = 1.25m,
        });

        Assert.Equal(3.75m, result.TaxAmount);
    }

    [Fact]
    public void PerVolume_MultipliesByVolume()
    {
        var method = new PerVolumeTaxCalculationMethod();

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 200m,
            Volume = 5m,
            FixedAmount = 0.5m,
        });

        Assert.Equal(2.5m, result.TaxAmount);
    }

    [Fact]
    public void TaxTable_UsesMatchingBracket()
    {
        var method = new TaxTableTaxCalculationMethod();

        var table = new TaxTable
        {
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m, Rate = 0.05m },
                new TaxTableRow { Minimum = 100m, Maximum = null, Rate = 0.08m, FixedAmount = 2m },
            ],
        };

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 150m,
            Table = table,
        });

        Assert.Equal((150m * 0.08m) + 2m, result.TaxAmount);
        Assert.Equal(0.08m, result.EffectiveRate);
    }

    [Fact]
    public void Progressive_TaxesEachBracketPortion()
    {
        var method = new ProgressiveTaxCalculationMethod();

        var table = new TaxTable
        {
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m, Rate = 0.10m },
                new TaxTableRow { Minimum = 100m, Maximum = 200m, Rate = 0.20m },
                new TaxTableRow { Minimum = 200m, Maximum = null, Rate = 0.30m },
            ],
        };

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 250m,
            Table = table,
        });

        // 100*0.10 + 100*0.20 + 50*0.30 = 10 + 20 + 15 = 45
        Assert.Equal(45m, result.TaxAmount);
    }

    [Fact]
    public void Threshold_TaxesExcessOverMinimum()
    {
        var method = new ThresholdTaxCalculationMethod();

        var table = new TaxTable
        {
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m, Rate = 0m },
                new TaxTableRow { Minimum = 100m, Maximum = null, Rate = 0.10m },
            ],
        };

        var result = method.Compute(new TaxComputationRequest
        {
            TaxableBase = 180m,
            Table = table,
        });

        // (180 - 100) * 0.10 = 8
        Assert.Equal(8m, result.TaxAmount);
    }
}
