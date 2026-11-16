using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// "Add income tax rule" offers the kinds of rule the features that are on register, and each kind's editor shows only
/// its own fields: Taxation's four, Form W-4 with United States Income Tax, and the CRA's and Revenu Québec's formulas
/// with Canada Income Tax.
/// </summary>
public sealed class IncomeTaxRuleSourceTests
{
    [Theory]
    [InlineData("Percentage", null, IncomeTaxRuleSources.FlatRate)]
    [InlineData(null, null, IncomeTaxRuleSources.FlatRate)]
    [InlineData("Percentage", "CA-EI", IncomeTaxRuleSources.ShareOfTax)]
    [InlineData("FixedAmount", null, IncomeTaxRuleSources.FixedAmount)]
    [InlineData("Progressive", null, IncomeTaxRuleSources.TaxTable)]
    [InlineData("TaxTable", null, IncomeTaxRuleSources.TaxTable)]
    [InlineData("UnitedStatesFormW4", null, "UnitedStatesFormW4")]
    public void ARuleSavedWithoutASource_GetsOneFromWhatItHolds(string method, string shareOf, string expected)
    {
        var rule = new IncomeTaxRule { Method = method, ShareOfTaxCode = shareOf };

        Assert.Equal(expected, IncomeTaxRuleSources.Infer(rule, name => name == "UnitedStatesFormW4"));
    }

    [Fact]
    public void IncomeTax_OffersTaxationsFourKinds_EachWithItsMethod()
    {
        var sources = Sources(services => new IncomeTaxStartup().ConfigureServices(services));

        Assert.Equal(
            [
                (IncomeTaxRuleSources.FlatRate, TaxCalculationMethodNames.Percentage),
                (IncomeTaxRuleSources.ShareOfTax, TaxCalculationMethodNames.Percentage),
                (IncomeTaxRuleSources.TaxTable, null),
                (IncomeTaxRuleSources.FixedAmount, TaxCalculationMethodNames.FixedAmount),
            ],
            sources.OrderBy(source => source.Position).Select(source => (source.Name, source.Method)));
    }

    [Fact]
    public void CountryPacks_AddTheirOwnKinds_OnlyWhenTheirFeatureIsOn()
    {
        var unitedStates = Sources(services => new UnitedStatesIncomeTaxStartup().ConfigureServices(services));
        var canada = Sources(services => new CanadaIncomeTaxStartup().ConfigureServices(services));

        // A United States site never sees Canada's formulas, and the other way round.
        Assert.Equal([UnitedStatesIncomeTax.FormW4Method], unitedStates.Select(source => source.Name));
        Assert.Equal([CanadaIncomeTax.FederalMethod, CanadaIncomeTax.ProvincialMethod, CanadaIncomeTax.QuebecMethod], canada.OrderBy(source => source.Position).Select(source => source.Name));
        Assert.All(unitedStates.Concat(canada), source => Assert.Equal(source.Name, source.Method));
    }

    [Fact]
    public void EachSourcesEditor_EditsOnlyItsOwnRules()
    {
        var flatRate = new IncomeTaxFlatRateDisplayDriver(new PassThroughStringLocalizer<IncomeTaxFlatRateDisplayDriver>());
        var share = new IncomeTaxShareOfTaxDisplayDriver(new PassThroughStringLocalizer<IncomeTaxShareOfTaxDisplayDriver>());
        var fixedAmount = new IncomeTaxFixedAmountDisplayDriver(new PassThroughStringLocalizer<IncomeTaxFixedAmountDisplayDriver>());

        Assert.True(flatRate.CanHandleModel(new IncomeTaxRule { Source = IncomeTaxRuleSources.FlatRate }));
        Assert.False(flatRate.CanHandleModel(new IncomeTaxRule { Source = IncomeTaxRuleSources.ShareOfTax }));
        Assert.True(share.CanHandleModel(new IncomeTaxRule { Source = IncomeTaxRuleSources.ShareOfTax }));
        Assert.False(share.CanHandleModel(new IncomeTaxRule { Source = UnitedStatesIncomeTax.FormW4Method }));
        Assert.True(fixedAmount.CanHandleModel(new IncomeTaxRule { Source = IncomeTaxRuleSources.FixedAmount }));
    }

    [Fact]
    public void TheWithholdingEditor_EditsTheRulesOfEverySourceWithAWithholdingMethodThatIsOn()
    {
        var options = Options.Create(new IncomeTaxRuleSourceOptions());

        options.Value.Sources[UnitedStatesIncomeTax.FormW4Method] = new IncomeTaxRuleSource { Name = UnitedStatesIncomeTax.FormW4Method, Method = UnitedStatesIncomeTax.FormW4Method };
        options.Value.Sources[CanadaIncomeTax.FederalMethod] = new IncomeTaxRuleSource { Name = CanadaIncomeTax.FederalMethod, Method = CanadaIncomeTax.FederalMethod };
        options.Value.Sources[IncomeTaxRuleSources.FlatRate] = new IncomeTaxRuleSource { Name = IncomeTaxRuleSources.FlatRate, Method = TaxCalculationMethodNames.Percentage };

        var driver = new IncomeTaxWithholdingDisplayDriver(
            options,
            [new UnitedStatesFormW4WithholdingMethod()],
            new Fakes.InMemoryNamedCatalog<TaxTable>(),
            new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizerFactory(),
            new PassThroughStringLocalizer<IncomeTaxWithholdingDisplayDriver>());

        Assert.True(driver.CanHandleModel(new IncomeTaxRule { Source = UnitedStatesIncomeTax.FormW4Method }));

        // Canada's source is registered but its method isn't on; a flat rate has no withholding method.
        Assert.False(driver.CanHandleModel(new IncomeTaxRule { Source = CanadaIncomeTax.FederalMethod }));
        Assert.False(driver.CanHandleModel(new IncomeTaxRule { Source = IncomeTaxRuleSources.FlatRate }));
    }

    private static List<IncomeTaxRuleSource> Sources(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();

        configure(services);

        using var provider = services.BuildServiceProvider();

        return [.. provider.GetRequiredService<IOptions<IncomeTaxRuleSourceOptions>>().Value.Sources.Values];
    }
}
