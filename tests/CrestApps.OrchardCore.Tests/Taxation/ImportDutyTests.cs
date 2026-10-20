using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// An import duty is charged at checkout only on orders worth more than the country's threshold, so a rule's threshold
/// can be the whole order's rather than each item's.
/// </summary>
public sealed class ImportDutyTests
{
    [Theory]
    [InlineData(TaxThresholdBasis.Order, 8)]
    [InlineData(TaxThresholdBasis.Item, 0)]
    public async Task AThreshold_IsTheWholeOrdersOrEachItems(TaxThresholdBasis basis, decimal duty)
    {
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Import duty",
            TaxType = TaxTypeNames.ImportDuty,
            TaxName = "Import duty",
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.04m,
            MinimumAmount = 150m,
            ThresholdBasis = basis,
        });

        // Two items of 100: each is under the threshold, the order of both is over it.
        var context = TaxTestData.Context(100m);
        context.Items.Add(new TaxableItem { Id = "item-2", Kind = TaxableItemKind.Physical, Quantity = 1m, UnitPrice = 100m, Currency = "USD" });

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(duty, result.TaxAmount);
        Assert.All(result.Lines, line => Assert.Equal(TaxTypeNames.ImportDuty, line.TaxType));
    }

    [Fact]
    public async Task AnOrderBelowTheThreshold_PaysNoDuty()
    {
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Import duty",
            TaxType = TaxTypeNames.ImportDuty,
            JurisdictionId = jurisdictionId,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.04m,
            MinimumAmount = 150m,
            ThresholdBasis = TaxThresholdBasis.Order,
        });

        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(60m, 2), TestContext.Current.CancellationToken);

        Assert.Equal(0m, result.TaxAmount);
    }
}
