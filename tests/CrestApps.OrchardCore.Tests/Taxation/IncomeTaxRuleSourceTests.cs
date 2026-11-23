using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using CrestApps.OrchardCore.Taxation.Core.Models;

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
    [InlineData("Threshold", null, IncomeTaxRuleSources.TaxTable)]
    [InlineData("UnitedStatesFormW4", null, "UnitedStatesFormW4")]
    [InlineData("CanadaFederal", null, "CanadaFederal")]
    public void ARuleSavedWithoutASource_GetsOneFromWhatItHolds(string method, string shareOf, string expected)
    {
        var rule = new IncomeTaxRule { Method = method, ShareOfTaxCode = shareOf };

        Assert.Equal(expected, IncomeTaxRuleSources.Infer(rule));
    }

    [Fact]
    public void ARuleOfAPackThatIsOff_KeepsItsKind_AndNoTaxationEditorTakesIt()
    {
        // Canada Income Tax is off: an old CanadaFederal rule is no tax table rule, so the table card never rewrites it.
        var rule = new IncomeTaxRule { Method = "CanadaFederal", Tables = new() { ["Federal"] = "t1" }, Parameters = new() { ["BasicAmount"] = 16129 } };

        rule.Source = IncomeTaxRuleSources.Infer(rule);

        var table = new IncomeTaxTableDisplayDriver(Options.Create(new TaxCalculationMethodOptions()), Mock.Of<ITaxCalculationMethodProvider>(), new Fakes.InMemoryNamedCatalog<TaxTable>(), new PassThroughStringLocalizer<IncomeTaxTableDisplayDriver>());
        var withholding = new IncomeTaxWithholdingDisplayDriver(Options.Create(new IncomeTaxRuleSourceOptions()), [], new Fakes.InMemoryNamedCatalog<TaxTable>(), new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizerFactory(), new PassThroughStringLocalizer<IncomeTaxWithholdingDisplayDriver>());

        Assert.Equal("CanadaFederal", rule.Source);
        Assert.False(table.CanHandleModel(rule));
        Assert.False(withholding.CanHandleModel(rule));
    }

    [Fact]
    public void ValuesASourceDoesntOwn_AreListed_AndCleared()
    {
        // A W-4 rule of an older layout that still holds a yearly maximum, which would cap the withholding unseen.
        var rule = new IncomeTaxRule { Method = "UnitedStatesFormW4", YearlyMaximum = 500, UsesElections = true, SupplementalRate = 22, ShareOfTaxCode = "US-X" };

        var dropped = IncomeTaxRuleSources.ValuesNotOwned(rule, IncomeTaxRuleSources.WithholdingFields);

        IncomeTaxRuleSources.ClearNotOwned(rule, IncomeTaxRuleSources.WithholdingFields, nameof(IncomeTaxRule.ShareOfTaxCode));

        Assert.Equal([nameof(IncomeTaxRule.YearlyMaximum), nameof(IncomeTaxRule.ShareOfTaxCode)], dropped);
        Assert.Null(rule.YearlyMaximum);
        Assert.True(rule.UsesElections);
        Assert.Equal(22, rule.SupplementalRate);
        Assert.Equal("US-X", rule.ShareOfTaxCode);
    }

    [Fact]
    public void APacksOwnDriver_CanOptOutOfTheWithholdingEditor_AndDeriveFromThePublicBase()
    {
        var options = Options.Create(new IncomeTaxRuleSourceOptions());

        options.Value.Sources["MyPack"] = new IncomeTaxRuleSource { Name = "MyPack", Method = UnitedStatesIncomeTax.FormW4Method, UsesWithholdingEditor = false };

        var withholding = new IncomeTaxWithholdingDisplayDriver(options, [new UnitedStatesFormW4WithholdingMethod()], new Fakes.InMemoryNamedCatalog<TaxTable>(), new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizerFactory(), new PassThroughStringLocalizer<IncomeTaxWithholdingDisplayDriver>());

        Assert.False(withholding.CanHandleModel(new IncomeTaxRule { Source = "MyPack" }));
        Assert.True(typeof(global::CrestApps.OrchardCore.Taxation.Core.Drivers.IncomeTaxRuleSourceDisplayDriver).IsPublic);
        Assert.Same(typeof(global::CrestApps.OrchardCore.Taxation.Core.Services.IncomeTaxRulesProvider).Assembly, typeof(global::CrestApps.OrchardCore.Taxation.Core.Drivers.IncomeTaxRuleSourceDisplayDriver).Assembly);
    }

    [Fact]
    public async Task TaxationMenus_WithCommerceOff_AddACommerceItemWithItsIdPositionAndIcon_LikeCommerce()
    {
        // Payroll alone: Tax Tables and Income Tax, no Commerce module. Each Commerce node is the Commerce module's
        // (id, position, icon), so the admin menu merges them into one, with or without Commerce.
        var builders = new List<global::OrchardCore.Navigation.NavigationBuilder>();

        foreach (var provider in new global::OrchardCore.Navigation.INavigationProvider[]
        {
            new TaxTablesAdminMenu(new PassThroughStringLocalizer<TaxTablesAdminMenu>()),
            new IncomeTaxAdminMenu(new PassThroughStringLocalizer<IncomeTaxAdminMenu>()),
            new global::CrestApps.OrchardCore.Commerce.Navigation.CommerceAdminMenu(new PassThroughStringLocalizer<global::CrestApps.OrchardCore.Commerce.Navigation.CommerceAdminMenu>()),
        })
        {
            var builder = new global::OrchardCore.Navigation.NavigationBuilder();

            await provider.BuildNavigationAsync(global::OrchardCore.Navigation.NavigationConstants.AdminId, builder);
            builders.Add(builder);
        }

        var commerceNodes = builders.Select(builder => Assert.Single(builder.Build(), item => item.Text.Value == "Commerce")).ToList();

        Assert.All(commerceNodes, node =>
        {
            Assert.Equal("commerce", node.Id);
            Assert.Equal("15", node.Position);
            Assert.Contains("icon-class-fa-solid", node.Classes);
            Assert.Contains("icon-class-fa-store", node.Classes);
        });
        Assert.Equal(["Jurisdictions", "Tables"], Assert.Single(commerceNodes[0].Items, item => item.Id == "taxation").Items.Select(item => item.Text.Value));
        Assert.Equal(["Income Tax Rules"], Assert.Single(commerceNodes[1].Items, item => item.Id == "taxation").Items.Select(item => item.Text.Value));
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
        Assert.Equal([UnitedStatesIncomeTax.FormW4Method, UnitedStatesIncomeTax.StateCertificateMethod], unitedStates.OrderBy(source => source.Position).Select(source => source.Name));
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
