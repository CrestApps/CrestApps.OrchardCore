using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using OrchardCore.DisplayManagement.Handlers;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxRuleDisplayDriverTests
{
    private static TaxRuleMethodDisplayDriver CreateDriver()
    {
        var provider = new DefaultTaxCalculationMethodProvider(
            new ITaxCalculationMethod[]
            {
                new PercentageTaxCalculationMethod(),
                new TaxTableTaxCalculationMethod(),
            });

        return new TaxRuleMethodDisplayDriver(
            provider,
            new InMemoryNamedCatalog<TaxTable>(),
            new PassThroughStringLocalizer<TaxRuleMethodDisplayDriver>());
    }

    private static UpdateEditorContext CreateUpdateContext(TaxRuleMethodViewModel submitted)
    {
        var updater = new FakeUpdateModel(submitted);

        return new UpdateEditorContext(null, string.Empty, false, string.Empty, null, null, updater);
    }

    [Fact]
    public async Task UpdateAsync_WhenSourceHasNoRegisteredMethod_DoesNotBindOrThrow()
    {
        // Arrange
        var driver = CreateDriver();
        var submitted = new TaxRuleMethodViewModel
        {
            Rate = 5,
            FixedAmount = 3,
            TaxTableId = "tt1",
        };
        var context = CreateUpdateContext(submitted);
        var rule = new TaxRule
        {
            Source = "does-not-exist",
        };

        // Act
        await driver.UpdateAsync(rule, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Null(rule.Rate);
        Assert.Null(rule.FixedAmount);
        Assert.Null(rule.TaxTableId);
    }

    [Fact]
    public async Task UpdateAsync_WhenTableMethodHasNoTaxTable_AddsModelError()
    {
        // Arrange
        var driver = CreateDriver();
        var submitted = new TaxRuleMethodViewModel
        {
            TaxTableId = string.Empty,
        };
        var context = CreateUpdateContext(submitted);
        var rule = new TaxRule
        {
            Source = TaxCalculationMethodNames.TaxTable,
        };

        // Act
        await driver.UpdateAsync(rule, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Null(rule.TaxTableId);
    }

    [Fact]
    public async Task UpdateAsync_WhenTableMethodHasTaxTable_KeepsTableAndClearsRateAndFixedAmount()
    {
        // Arrange
        var driver = CreateDriver();
        var submitted = new TaxRuleMethodViewModel
        {
            TaxTableId = "tt-42",
            Rate = 7,
            FixedAmount = 9,
        };
        var context = CreateUpdateContext(submitted);
        var rule = new TaxRule
        {
            Source = TaxCalculationMethodNames.TaxTable,
        };

        // Act
        await driver.UpdateAsync(rule, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("tt-42", rule.TaxTableId);
        Assert.Null(rule.Rate);
        Assert.Null(rule.FixedAmount);
    }

    [Fact]
    public async Task UpdateAsync_WhenPercentageMethod_KeepsRateAndClearsFixedAmountAndTaxTable()
    {
        // Arrange
        var driver = CreateDriver();
        var submitted = new TaxRuleMethodViewModel
        {
            Rate = 12,
            FixedAmount = 4,
            TaxTableId = "tt-should-clear",
        };
        var context = CreateUpdateContext(submitted);
        var rule = new TaxRule
        {
            Source = TaxCalculationMethodNames.Percentage,
        };

        // Act
        await driver.UpdateAsync(rule, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(12m, rule.Rate);
        Assert.Null(rule.FixedAmount);
        Assert.Null(rule.TaxTableId);
    }
}
