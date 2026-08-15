using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxRoundingTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    private static async Task SeedTwoRulesAsync(TaxTestHarness harness)
    {
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "State",
            TaxType = "STATE",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.0825m,
        });

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "County",
            TaxType = "COUNTY",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.0825m,
        });
    }

    [Fact]
    public async Task LineRounding_RoundsEachLineIndependently()
    {
        var harness = CreateHarness();
        await SeedTwoRulesAsync(harness);

        var context = TaxTestData.Context(10.10m);
        context.RoundingLevel = TaxRoundingLevel.Line;

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Each line: round(10.10 * 0.0825) = round(0.83325) = 0.83; total = 1.66.
        Assert.All(result.Lines, line => Assert.Equal(0.83m, line.TaxAmount));
        Assert.Equal(1.66m, result.TaxAmount);
    }

    [Fact]
    public async Task TransactionRounding_RoundsAggregatedTotal()
    {
        var harness = CreateHarness();
        await SeedTwoRulesAsync(harness);

        var context = TaxTestData.Context(10.10m);
        context.RoundingLevel = TaxRoundingLevel.Transaction;

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Unrounded total: 2 * 0.83325 = 1.6665; rounded to 1.67.
        Assert.Equal(1.67m, result.TaxAmount);
    }
}
