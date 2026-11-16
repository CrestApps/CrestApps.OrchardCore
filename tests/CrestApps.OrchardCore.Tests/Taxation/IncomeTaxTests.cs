using System.Reflection;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Moq;
using OrchardCore.Modules.Manifest;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Income tax belongs to Taxation: the income tax rules, the provider seam, withholding methods, tax categories and
/// Form W-4 live in Taxation's assemblies, never see a Payroll type, and work from a neutral request.
/// </summary>
public sealed class IncomeTaxTests
{
    private static readonly Assembly[] _taxationAssemblies =
    [
        typeof(TaxationConstants).Assembly,
        typeof(IncomeTaxRulesProvider).Assembly,
        typeof(global::CrestApps.OrchardCore.Taxation.Startup).Assembly,
    ];

    [Fact]
    public void TaxationAssemblies_NeverReferenceAPayrollAssembly()
    {
        foreach (var assembly in _taxationAssemblies)
        {
            var payroll = assembly.GetReferencedAssemblies()
                .Where(reference => reference.Name.StartsWith("CrestApps.OrchardCore.Payroll", StringComparison.Ordinal))
                .Select(reference => reference.Name);

            Assert.Empty(payroll);
        }
    }

    [Fact]
    public void IncomeTaxTypes_LiveInTaxation()
    {
        Type[] types =
        [
            typeof(IncomeTaxRule),
            typeof(IncomeTaxCategories),
            typeof(IIncomeTaxProvider),
            typeof(IncomeTaxRequest),
            typeof(IncomeTaxResult),
            typeof(IncomeTaxLine),
            typeof(IIncomeTaxWithholdingMethod),
            typeof(IIncomeTaxRuleManager),
            typeof(IncomeTaxCategoryOptions),
            typeof(IncomeTaxElectionOptions),
            typeof(ElectionFieldSet),
            typeof(IncomeTaxRulesProvider),
            typeof(UnitedStatesFormW4WithholdingMethod),
        ];

        Assert.All(types, type => Assert.Contains(type.Assembly, _taxationAssemblies));
    }

    [Fact]
    public void TaxationFeatures_SalesTaxKeepsItsId_IncomeTaxDependsOnTaxTablesOnly()
    {
        var features = Features(typeof(global::CrestApps.OrchardCore.Taxation.Startup).Assembly);

        Assert.Equal("Sales Tax", features[TaxationConstants.Feature.Taxation].Name);
        Assert.Equal("CrestApps.OrchardCore.Taxation", TaxationConstants.Feature.Taxation);
        Assert.Equal([TaxationConstants.Feature.Tables], features[TaxationConstants.Feature.IncomeTax].Dependencies);
        Assert.Equal("Finance", features[TaxationConstants.Feature.IncomeTax].Category);
        Assert.Equal("CrestApps.OrchardCore.Taxation.IncomeTax", TaxationConstants.Feature.IncomeTax);
        Assert.Equal("CrestApps.OrchardCore.Taxation.IncomeTax.UnitedStates", TaxationConstants.Feature.IncomeTaxUnitedStates);
        Assert.Contains(TaxationConstants.Feature.IncomeTax, features[TaxationConstants.Feature.IncomeTaxUnitedStates].Dependencies);
    }

    [Fact]
    public void PayrollFeatures_DependOnTaxationIncomeTax_AndKeepOnlyTheFilings()
    {
        var features = Features(typeof(global::CrestApps.OrchardCore.Payroll.PaySetupStartup).Assembly);

        Assert.Contains(TaxationConstants.Feature.IncomeTax, features[PayrollConstants.Features.Payroll].Dependencies);
        Assert.DoesNotContain(features.Keys, id => id == "CrestApps.OrchardCore.Payroll.TaxTables");

        var filings = features[PayrollConstants.Features.UnitedStates];

        Assert.Equal("United States Payroll Filings", filings.Name);
        Assert.Contains(PayrollConstants.Features.Payroll, filings.Dependencies);
        Assert.Contains(TaxationConstants.Feature.IncomeTaxUnitedStates, filings.Dependencies);
    }

    [Fact]
    public void IncomeTaxFeature_CreatesTheIncomeTaxCollection()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        new IncomeTaxStartup().ConfigureServices(services);

        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);

        // Without it, a site's first read of the rules fails: no IncomeTax_Document table.
        Assert.Contains(TaxationConstants.IncomeTaxCollectionName, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.Options.IOptions<global::OrchardCore.Data.StoreCollectionOptions>>(provider).Value.Collections);
    }

    [Fact]
    public async Task TheBuiltInProvider_WorksFromANeutralRequest()
    {
        var rules = new[]
        {
            new IncomeTaxRule { ItemId = "ss", TaxCode = "US-SS-EE", Name = "Social Security", Jurisdiction = "US", Category = IncomeTaxCategories.SocialInsurance, Method = "Percentage", Rate = 6.2m, WageBase = 184500, EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { ItemId = "er", TaxCode = "US-SS-ER", Name = "Social Security (employer)", Jurisdiction = "US", Category = IncomeTaxCategories.SocialInsurance, Payer = IncomeTaxPayer.Employer, Method = "Percentage", Rate = 6.2m, EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { ItemId = "ca", TaxCode = "US-CA-SDI", Name = "California SDI", Jurisdiction = "US-CA", Category = IncomeTaxCategories.SocialInsurance, Method = "Percentage", Rate = 1, EffectiveFrom = new DateOnly(2026, 1, 1) },
        };
        var manager = new Mock<IIncomeTaxRuleManager>();

        manager.Setup(candidate => candidate.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

        var provider = new IncomeTaxRulesProvider(manager.Object, new DefaultTaxCalculationMethodProvider([new PercentageTaxCalculationMethod()]), new InMemoryNamedCatalog<TaxTable>(), [], new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<IncomeTaxRulesProvider>());
        var request = new IncomeTaxRequest
        {
            PayerId = "employer",
            RegisteredJurisdictions = ["US"],
            IncomeKind = IncomeKind.Employment,
            PayDate = new DateOnly(2026, 11, 20),
            PeriodsPerYear = 26,
            Income = new Dictionary<string, decimal> { [IncomeTaxCategories.SocialInsurance] = 1000 },
            TaxesToDate = new Dictionary<string, IncomeTaxToDate> { ["US-SS-EE"] = new() { YearTaxableIncome = 184000 } },
        };

        var result = await provider.CalculateAsync(request, TestContext.Current.CancellationToken);

        // 500 left under the wage base at 6.2%, the employer's share on all 1000, and nothing for a jurisdiction the
        // employer isn't registered in.
        Assert.Equal(
            [("US-SS-EE", IncomeTaxPayer.Employee, 500m, 31m), ("US-SS-ER", IncomeTaxPayer.Employer, 1000m, 62m)],
            result.Lines.Select(line => (line.TaxCode, line.Payer, line.TaxableIncome, line.Amount)));

        request.IncomeKind = IncomeKind.Contractor;

        Assert.Empty((await provider.CalculateAsync(request, TestContext.Current.CancellationToken)).Lines);
    }

    [Fact]
    public async Task ARuleOnThePayersOwnRate_TakesItFromTheRegistration_AndNeedsOne()
    {
        var unemployment = new IncomeTaxRule { ItemId = "sui", TaxCode = "US-TX-SUI", Name = "Texas unemployment", Jurisdiction = "US-TX", Category = IncomeTaxCategories.Unemployment, Payer = IncomeTaxPayer.Employer, Method = "Percentage", RateFromRegistration = true, EffectiveFrom = new DateOnly(2026, 1, 1) };
        var manager = new Mock<IIncomeTaxRuleManager>();

        manager.Setup(candidate => candidate.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([unemployment]);

        var provider = new IncomeTaxRulesProvider(manager.Object, new DefaultTaxCalculationMethodProvider([new PercentageTaxCalculationMethod()]), new InMemoryNamedCatalog<TaxTable>(), [], new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<IncomeTaxRulesProvider>());
        var request = new IncomeTaxRequest
        {
            Registrations = [new IncomeTaxRegistration { Jurisdiction = "US-TX", AccountId = "99-999999-9", Rate = 2.7m }],
            HomeJurisdictions = ["US-TX"],
            IncomeKind = IncomeKind.Employment,
            PayDate = new DateOnly(2026, 11, 20),
            PeriodsPerYear = 26,
            Income = new Dictionary<string, decimal> { [IncomeTaxCategories.Unemployment] = 1000 },
        };

        // The registration alone makes the employer registered there, and its rate applies: 2.7% of 1000.
        var line = Assert.Single((await provider.CalculateAsync(request, TestContext.Current.CancellationToken)).Lines);

        Assert.Equal(27m, line.Amount);
        Assert.Null(unemployment.Rate);

        // No rate on the registration and none on the rule: not worked out, and said so.
        request.Registrations[0].Rate = null;

        var result = await provider.CalculateAsync(request, TestContext.Current.CancellationToken);

        Assert.Empty(result.Lines);
        Assert.Contains("no rate on its US-TX registration", Assert.Single(result.Warnings), StringComparison.Ordinal);

        // A rate on the rule is the fallback.
        unemployment.Rate = 3.1m;

        Assert.Equal(31m, Assert.Single((await provider.CalculateAsync(request, TestContext.Current.CancellationToken)).Lines).Amount);
    }

    private static Dictionary<string, FeatureAttribute> Features(Assembly assembly)
        => assembly.GetCustomAttributes<FeatureAttribute>().Where(feature => feature.GetType() == typeof(FeatureAttribute)).ToDictionary(feature => feature.Id, StringComparer.Ordinal);
}
