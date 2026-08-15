using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Verifies refunds are derived from the original immutable tax snapshot and never recalculated with
/// current rules, including partial (proportional) refunds allocated across the original tax lines.
/// </summary>
public sealed class TaxRefundCalculatorTests
{
    private static ITaxRefundCalculator CreateCalculator()
        => new TaxTestHarness(new TestClock(TaxTestData.TransactionDate)).GetService<ITaxRefundCalculator>();

    private static TaxSnapshot CreateSnapshot()
        => new()
        {
            Currency = "USD",
            TaxableAmount = 100m,
            TaxAmount = 8m,
            TotalAmount = 108m,
            Lines =
            [
                new TaxLine
                {
                    TaxName = "State",
                    JurisdictionName = "California",
                    Rate = 0.06m,
                    TaxableAmount = 100m,
                    TaxAmount = 6m,
                },
                new TaxLine
                {
                    TaxName = "County",
                    JurisdictionName = "Los Angeles",
                    Rate = 0.02m,
                    TaxableAmount = 100m,
                    TaxAmount = 2m,
                },
            ],
        };

    [Fact]
    public void FullRefund_ReproducesSnapshotAmounts()
    {
        var result = CreateCalculator().CalculateFullRefund(CreateSnapshot());

        Assert.Equal("USD", result.Currency);
        Assert.Equal(100m, result.RefundedTaxableAmount);
        Assert.Equal(8m, result.RefundedTaxAmount);
        Assert.Equal(108m, result.RefundedTotalAmount);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(6m, result.Lines[0].TaxAmount);
        Assert.Equal(2m, result.Lines[1].TaxAmount);
    }

    [Fact]
    public void ProportionalRefund_RefundsHalf_AllocatesTaxAcrossLines()
    {
        // Refund half of the $108 total ($54) => half the tax ($4), split $3 + $1 across the lines.
        var result = CreateCalculator().CalculateProportionalRefund(CreateSnapshot(), 54m);

        Assert.Equal(54m, result.RefundedTotalAmount);
        Assert.Equal(50m, result.RefundedTaxableAmount);
        Assert.Equal(4m, result.RefundedTaxAmount);
        Assert.Equal(3m, result.Lines[0].TaxAmount);
        Assert.Equal(1m, result.Lines[1].TaxAmount);
    }

    [Fact]
    public void ProportionalRefund_AtOrAboveTotal_IsFullRefund()
    {
        var result = CreateCalculator().CalculateProportionalRefund(CreateSnapshot(), 200m);

        Assert.Equal(8m, result.RefundedTaxAmount);
        Assert.Equal(108m, result.RefundedTotalAmount);
    }

    [Fact]
    public void ProportionalRefund_NonPositive_RefundsNothing()
    {
        var result = CreateCalculator().CalculateProportionalRefund(CreateSnapshot(), 0m);

        Assert.Equal(0m, result.RefundedTaxAmount);
        Assert.Equal(0m, result.RefundedTotalAmount);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void ProportionalRefund_UsesHistoricalRate_NotCurrentRules()
    {
        // The snapshot rate (6% + 2%) is used regardless of any current rules; the calculator never
        // consults the tax engine, so a later rate change cannot alter this refund.
        var snapshot = CreateSnapshot();

        var result = CreateCalculator().CalculateProportionalRefund(snapshot, 108m);

        Assert.Equal(snapshot.TaxAmount, result.RefundedTaxAmount);
    }
}
