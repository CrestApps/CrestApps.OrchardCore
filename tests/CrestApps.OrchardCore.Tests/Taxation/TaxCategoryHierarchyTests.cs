using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// A rule for a category also covers the categories under it, but never the other way around.
/// </summary>
public sealed class TaxCategoryHierarchyTests
{
    [Fact]
    public async Task Calculate_ARuleForTheParentCategory_TaxesAnItemInAChildCategory()
    {
        // Arrange: Television sits under Electronics, and the rule is for Electronics.
        var harness = await CreateHarnessAsync();
        await AddRuleAsync(harness, "ELECTRONICS");

        // Act
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m, categoryCode: "TELEVISION"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20m, Assert.Single(result.Lines).TaxAmount);
    }

    [Fact]
    public async Task Calculate_ARuleForAGrandparentCategory_TaxesAnItemTwoLevelsDown()
    {
        // Arrange
        var harness = await CreateHarnessAsync();
        await AddRuleAsync(harness, "GOODS");

        // Act
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m, categoryCode: "television"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20m, Assert.Single(result.Lines).TaxAmount);
    }

    [Fact]
    public async Task Calculate_ARuleForAChildCategory_DoesNotTaxAnItemInTheParent()
    {
        // Arrange
        var harness = await CreateHarnessAsync();
        await AddRuleAsync(harness, "TELEVISION");

        // Act
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m, categoryCode: "ELECTRONICS"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Calculate_WhenCategoryParentsFormALoop_StillFinishes()
    {
        // Arrange: A's parent is B and B's parent is A.
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        await harness.Categories.CreateAsync(new TaxCategory { Name = "A", Code = "A", ParentCode = "B" }, TestContext.Current.CancellationToken);
        await harness.Categories.CreateAsync(new TaxCategory { Name = "B", Code = "B", ParentCode = "A" }, TestContext.Current.CancellationToken);
        await AddRuleAsync(harness, "B");

        // Act
        var result = await harness.TaxService.CalculateAsync(TaxTestData.Context(200m, categoryCode: "A"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20m, Assert.Single(result.Lines).TaxAmount);
    }

    private static async Task<TaxTestHarness> CreateHarnessAsync()
    {
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));

        await harness.Categories.CreateAsync(new TaxCategory { Name = "Goods", Code = "GOODS" });
        await harness.Categories.CreateAsync(new TaxCategory { Name = "Electronics", Code = "ELECTRONICS", ParentCode = "GOODS" });
        await harness.Categories.CreateAsync(new TaxCategory { Name = "Television", Code = "TELEVISION", ParentCode = "ELECTRONICS" });

        return harness;
    }

    private static Task<string> AddRuleAsync(TaxTestHarness harness, string categoryCode)
        => TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = categoryCode + " tax",
            TaxType = TaxTypeNames.SalesTax,
            TaxName = categoryCode + " tax",
            TaxCode = categoryCode,
            CategoryCode = categoryCode,
            Source = TaxCalculationMethodNames.Percentage,
            Rate = 0.10m,
        });
}
