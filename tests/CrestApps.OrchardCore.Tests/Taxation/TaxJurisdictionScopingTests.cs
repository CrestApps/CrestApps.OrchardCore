using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// A rule written for a jurisdiction must only tax buyers in that jurisdiction. When the buyer's jurisdiction is
/// unknown (the checkout supplies no address) or outside it, the rule must not apply; otherwise every enabled rule
/// for a category taxes everyone, wherever they are.
/// </summary>
public sealed class TaxJurisdictionScopingTests
{
    [Fact]
    public async Task Calculate_ForABuyerOutsideTheRulesJurisdiction_DoesNotApplyIt()
    {
        // Arrange: a California rule, and a buyer in Texas.
        var harness = CreateHarness();
        await TaxTestData.AddJurisdictionAsync(harness, "Texas", "US", "TX");
        await AddCaliforniaRuleAsync(harness);

        var context = TaxTestData.Context(200m, destination: new Address { Country = "US", Region = "TX", City = "Austin", PostalCode = "73301" });

        // Act
        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public async Task Calculate_WithoutAnAddress_DoesNotApplyARuleForAJurisdiction()
    {
        // Arrange: the built-in checkout supplies no address, so no jurisdiction is resolved.
        var harness = CreateHarness();
        await AddCaliforniaRuleAsync(harness);

        var context = TaxTestData.Context(200m);
        context.Destination = null;

        // Act
        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public async Task Calculate_WithoutAnAddress_StillAppliesARuleWithNoJurisdiction()
    {
        // Arrange: a rule that applies everywhere.
        var harness = CreateHarness();
        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Flat levy",
            TaxType = TaxTypeNames.SalesTax,
            TaxName = "Flat levy",
            TaxCode = "FLAT",
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.05m,
        });

        var context = TaxTestData.Context(200m);
        context.Destination = null;

        // Act
        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(10m, Assert.Single(result.Lines).TaxAmount);
    }

    [Fact]
    public async Task Calculate_ForABuyerInTheRulesJurisdiction_AppliesIt()
    {
        // Arrange
        var harness = CreateHarness();
        await AddCaliforniaRuleAsync(harness);

        // Act: the default context ships to California.
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(15m, Assert.Single(result.Lines).TaxAmount);
    }

    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate));

    private static async Task AddCaliforniaRuleAsync(TaxTestHarness harness)
    {
        var california = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "CA Sales Tax",
            TaxType = TaxTypeNames.SalesTax,
            TaxName = "CA Sales Tax",
            TaxCode = "US-CA-SALES",
            JurisdictionId = california,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });
    }
}
