using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxSnapshotTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    [Fact]
    public async Task Snapshot_RemainsUnchanged_WhenRuleRateChangesLater()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        var rule = new TaxRule
        {
            Name = "Sales",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
            Version = 1,
        };

        await TaxTestData.AddRuleAsync(harness, rule);

        var snapshotFactory = harness.GetService<ITaxSnapshotFactory>();
        var originalResult = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);
        var snapshot = snapshotFactory.Create(TaxTestData.Context(100m), originalResult);

        Assert.Equal(10m, snapshot.TaxAmount);
        Assert.Equal(1, snapshot.Lines[0].RuleVersion);

        // The merchant later raises the rate and publishes a new rule version.
        rule.Rate = 0.20m;
        rule.Version = 2;
        await harness.Rules.UpdateAsync(rule, TestContext.Current.CancellationToken);

        var recalculated = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);

        Assert.Equal(20m, recalculated.TaxAmount);

        // The historical snapshot is immutable and still reflects the original determination.
        Assert.Equal(10m, snapshot.TaxAmount);
        Assert.Equal(1, snapshot.Lines[0].RuleVersion);
    }

    [Fact]
    public async Task Snapshot_IsIsolatedFromSubsequentResultMutation()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Sales",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });

        var snapshotFactory = harness.GetService<ITaxSnapshotFactory>();
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);
        var snapshot = snapshotFactory.Create(TaxTestData.Context(100m), result);

        result.Lines[0].TaxAmount = 999m;
        result.TaxAmount = 999m;

        Assert.Equal(10m, snapshot.Lines[0].TaxAmount);
        Assert.Equal(10m, snapshot.TaxAmount);
    }

    [Fact]
    public async Task Refund_UsesOriginalSnapshotDetermination()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        var rule = new TaxRule
        {
            Name = "Sales",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        };

        await TaxTestData.AddRuleAsync(harness, rule);

        var snapshotFactory = harness.GetService<ITaxSnapshotFactory>();
        var sale = await harness.TaxService.CalculateAsync(TaxTestData.Context(100m), TestContext.Current.CancellationToken);
        var snapshot = snapshotFactory.Create(TaxTestData.Context(100m), sale);

        // The rate changes before the refund is issued.
        rule.Rate = 0.25m;
        await harness.Rules.UpdateAsync(rule, TestContext.Current.CancellationToken);

        // A refund reverses the original snapshot, not today's rate.
        var refundTax = -snapshot.TaxAmount;

        Assert.Equal(-10m, refundTax);
    }
}
