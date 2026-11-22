using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The United States pack's state and local withholding: the state certificate's annualized method (Michigan,
/// Massachusetts with its deduction of Social Security and Medicare and its paid leave contribution, Virginia), a city's
/// tax with the state certificate's exemptions at a resident or a nonresident rate with only one city applying, the
/// nonresident alien amount of Form W-4, the payer's registration by tax category, and the verification of a rule. The
/// figures are the 2025 ones of each state's withholding guide; every expected amount is worked by hand below.
/// </summary>
public sealed class UnitedStatesStateWithholdingTests
{
    private static readonly DateOnly _payDate = new(2025, 11, 20);

    [Fact]
    public async Task Michigan_TakesTheExemptionsAndAddsTheExtra()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [await MichiganAsync(tables)]);

        // 2,000 x 26 = 52,000 - 2 x 5,800 = 40,400 at 4.25% = 1,717 / 26 = 66.04, plus 10.
        Assert.Equal(76.04m, await TaxAsync(provider, Request(2000m, 26, "US-MI", elections: Election("US-MI", ("Exemptions", "2"), ("ExtraWithholding", "10"))), "US-MI-SIT"));

        // Exempt: nothing withheld.
        Assert.Equal(0m, await TaxAsync(provider, Request(2000m, 26, "US-MI", elections: Election("US-MI", ("Exempt", "true"))), "US-MI-SIT"));

        // 100 x 26 = 2,600 is under 3 x 5,800.
        Assert.Equal(0m, await TaxAsync(provider, Request(100m, 26, "US-MI", elections: Election("US-MI", ("Exemptions", "3"))), "US-MI-SIT"));
    }

    [Fact]
    public async Task Massachusetts_DeductsSocialSecurityAndMedicareUpTo2000_AndTheM4Exemptions()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [.. Federal(), await MassachusettsAsync(tables)]);

        // 52,000 a year - 2,000 of Social Security and Medicare (153 x 26 = 3,978 withheld) - 4,400 - 1,000 = 44,600 at
        // 5% = 2,230 / 26.
        Assert.Equal(85.77m, await TaxAsync(provider, Request(2000m, 26, "US-MA", federal: true, elections: Election("US-MA", ("Exemptions", "2"))), "US-MA-SIT"));

        // A head of household takes 2,400 more: 52,000 - 2,000 - 4,400 - 2,400 = 43,200 at 5% = 2,160 / 26.
        Assert.Equal(83.08m, await TaxAsync(provider, Request(2000m, 26, "US-MA", federal: true, elections: Election("US-MA", ("Exemptions", "1"), ("HeadOfHousehold", "true"))), "US-MA-SIT"));

        // No-tax status: 150 x 52 = 7,800 a year is under 8,000.
        Assert.Equal(0m, await TaxAsync(provider, Request(150m, 52, "US-MA", federal: true, elections: Election("US-MA", ("Exemptions", "1"))), "US-MA-SIT"));
    }

    [Fact]
    public async Task Massachusetts_DeductsOnlyTheTaxesTheRuleNames()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var state = await MassachusettsAsync(tables);

        state.Parameters.Remove(UnitedStatesStateCertificateParameters.DeductedTaxesLimit);

        var provider = await ProviderAsync(tables, [.. Federal(), .. PaidLeave(), state]);

        // With no limit, all of the 153 of Social Security and Medicare, never the 9.20 of paid leave: 52,000 - 3,978 -
        // 4,400 - 1,000 = 42,622 at 5% = 2,131.10 / 26 = 81.97.
        Assert.Equal(81.97m, await TaxAsync(provider, Request(2000m, 26, "US-MA", federal: true, elections: Election("US-MA", ("Exemptions", "2"))), "US-MA-SIT"));
    }

    [Fact]
    public async Task Massachusetts_TheSurtax_IsNinePercentAboveTheThreshold()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [await MassachusettsAsync(tables)]);

        // 1,083,150 at 5% = 54,157.50, and 116,850 at 9% = 10,516.50: 64,674 / 12.
        Assert.Equal(5389.50m, await TaxAsync(provider, Request(100000m, 12, "US-MA"), "US-MA-SIT"));
    }

    [Fact]
    public async Task Massachusetts_PaidLeave_IsAFlatRateUpToItsWageBase()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, PaidLeave());
        var request = Request(2000m, 26, "US-MA");

        Assert.Equal(9.20m, await TaxAsync(provider, request, "US-MA-PFML-EE"));
        Assert.Equal(8.40m, await TaxAsync(provider, request, "US-MA-PFML-ER"));

        // 176,000 of the year's 176,100 already taxed: 100 left.
        request.TaxesToDate["US-MA-PFML-EE"] = new IncomeTaxToDate { YearTaxableIncome = 176000m };
        request.TaxesToDate["US-MA-PFML-ER"] = new IncomeTaxToDate { YearTaxableIncome = 176000m };

        Assert.Equal(0.46m, await TaxAsync(provider, request, "US-MA-PFML-EE"));
        Assert.Equal(0.42m, await TaxAsync(provider, request, "US-MA-PFML-ER"));
    }

    [Fact]
    public async Task Virginia_TakesTheStandardDeductionAndTheExemptions_ByBrackets()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [await VirginiaAsync(tables)]);

        // 52,000 - 8,750 - 2 x 930 - 800 = 40,590: 60 + 60 + 600 + 23,590 at 5.75% = 2,076.43 / 26.
        Assert.Equal(79.86m, await TaxAsync(provider, Request(2000m, 26, "US-VA", elections: Election("US-VA", ("Exemptions", "2"), ("SpecialExemptions", "1"))), "US-VA-SIT"));
    }

    [Fact]
    public async Task Detroit_IsTheResidentRateAtHome_ElseTheNonresidentRateWhereThePersonWorks()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [await MichiganAsync(tables), .. Cities()]);
        var certificate = Election("US-MI", ("Exemptions", "1"));

        // A Detroit resident working in Southfield: 2,000 - 600 / 26 = 1,976.92 at 2.4% = 47.45.
        var resident = await LinesAsync(provider, Michigan(2000m, ["US-MI-DETROIT"], ["US-MI-SOUTHFIELD"], certificate));

        Assert.Equal(47.45m, Amount(resident, "US-MI-DETROIT-R"));
        Assert.DoesNotContain(resident, line => line.TaxCode == "US-MI-DETROIT-NR");

        // A Dearborn resident working in Detroit: at 1.2% = 23.72.
        var nonresident = await LinesAsync(provider, Michigan(2000m, ["US-MI-DEARBORN"], ["US-MI-DETROIT"], certificate));

        Assert.Equal(23.72m, Amount(nonresident, "US-MI-DETROIT-NR"));
        Assert.DoesNotContain(nonresident, line => line.TaxCode == "US-MI-DETROIT-R");

        // Neither home nor work in a taxing city: no city tax.
        Assert.DoesNotContain(await LinesAsync(provider, Michigan(2000m, ["US-MI-DEARBORN"], ["US-MI-SOUTHFIELD"], certificate)), line => line.TaxCode.StartsWith("US-MI-DETROIT", StringComparison.Ordinal));

        // Living and working in Detroit: the resident rate only.
        var both = await LinesAsync(provider, Michigan(2000m, ["US-MI-DETROIT"], ["US-MI-DETROIT"], certificate));

        Assert.Equal(["US-MI-DETROIT-R"], both.Where(line => line.Jurisdiction == "US-MI-DETROIT").Select(line => line.TaxCode));
    }

    [Fact]
    public async Task OnlyOneCityTaxApplies_TheHomeCitysResidentRateBeforeTheWorkCitysNonresidentRate()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [await MichiganAsync(tables), .. Cities()]);

        // A Grand Rapids resident working in Detroit pays Grand Rapids' resident rate alone: 2,000 - 600 / 26 at 1.5%.
        var lines = await LinesAsync(provider, Michigan(2000m, ["US-MI-GRANDRAPIDS"], ["US-MI-DETROIT"], Election("US-MI", ("Exemptions", "1"))));

        Assert.Equal(["US-MI-GRANDRAPIDS-R"], lines.Where(line => line.Jurisdiction.Length > "US-MI".Length).Select(line => line.TaxCode));
        Assert.Equal(29.65m, Amount(lines, "US-MI-GRANDRAPIDS-R"));
    }

    [Fact]
    public async Task ACityTax_ReadsTheStateCertificatesExemptions_ButNotItsExemptionOrExtra()
    {
        var tables = new InMemoryNamedCatalog<TaxTable>();
        var provider = await ProviderAsync(tables, [await MichiganAsync(tables), .. Cities()]);

        // The paycheck of the Almohaseb vector: 1,850 with one exemption. Michigan: 48,100 - 5,800 = 42,300 at 4.25% =
        // 1,797.75 / 26 = 69.14; Detroit: 1,850 - 600 / 26 = 1,826.92 at 2.4% = 43.85.
        var paycheck = await LinesAsync(provider, Michigan(1850m, ["US-MI-DETROIT"], ["US-MI-DETROIT"], Election("US-MI", ("Exemptions", "1"))));

        Assert.Equal((69.14m, 43.85m), (Amount(paycheck, "US-MI-SIT"), Amount(paycheck, "US-MI-DETROIT-R")));

        // Exempt from Michigan's withholding: Detroit's tax still applies, with no exemptions and no extra: 2.4% of 2,000.
        var exempt = await LinesAsync(provider, Michigan(2000m, ["US-MI-DETROIT"], [], Election("US-MI", ("Exemptions", "2"), ("Exempt", "true"), ("ExtraWithholding", "10"))));

        Assert.DoesNotContain(exempt, line => line.TaxCode == "US-MI-SIT" && line.Amount > 0);
        Assert.Equal(48.00m, Amount(exempt, "US-MI-DETROIT-R"));
    }

    [Fact]
    public void Residency_AnyIsTheDefault_AResidentNeedsAHome_ANonresidentWorkAndNoHome()
    {
        var request = new IncomeTaxRequest { HomeJurisdictions = ["US", "US-MI", "US-MI-DETROIT"], WorkJurisdictions = ["US-MI", "US-MI-SOUTHFIELD"] };

        Assert.True(IncomeTaxRulesProvider.AppliesToResidency(new IncomeTaxRule { Jurisdiction = "US-MI-SOUTHFIELD" }, request));
        Assert.True(IncomeTaxRulesProvider.AppliesToResidency(new IncomeTaxRule { Jurisdiction = "US-MI-DETROIT", Residency = IncomeTaxResidency.Resident }, request));
        Assert.False(IncomeTaxRulesProvider.AppliesToResidency(new IncomeTaxRule { Jurisdiction = "US-MI-SOUTHFIELD", Residency = IncomeTaxResidency.Resident }, request));
        Assert.True(IncomeTaxRulesProvider.AppliesToResidency(new IncomeTaxRule { Jurisdiction = "US-MI-SOUTHFIELD", Residency = IncomeTaxResidency.Nonresident }, request));
        Assert.False(IncomeTaxRulesProvider.AppliesToResidency(new IncomeTaxRule { Jurisdiction = "US-MI", Residency = IncomeTaxResidency.Nonresident }, request));
        Assert.False(IncomeTaxRulesProvider.AppliesToResidency(new IncomeTaxRule { Jurisdiction = "US-MI-DEARBORN", Residency = IncomeTaxResidency.Nonresident }, request));
    }

    [Fact]
    public async Task ARuleOnThePayersOwnRate_ReadsTheRegistrationOfItsCategory_ElseTheFirst()
    {
        var unemployment = new IncomeTaxRule { ItemId = "sui", TaxCode = "US-MI-SUI", Name = "Michigan unemployment", Jurisdiction = "US-MI", Category = IncomeTaxCategories.Unemployment, Payer = IncomeTaxPayer.Employer, Method = TaxCalculationMethodNames.Percentage, RateFromRegistration = true, EffectiveFrom = new DateOnly(2025, 1, 1) };
        var provider = await ProviderAsync(new InMemoryNamedCatalog<TaxTable>(), [unemployment]);
        var request = Request(1000m, 26, "US-MI");

        // The withholding account comes first and has no rate; the unemployment account's 2.7% applies.
        request.Registrations =
        [
            new IncomeTaxRegistration { Jurisdiction = "US-MI", AccountId = "WH-1", Categories = [IncomeTaxCategories.IncomeTax] },
            new IncomeTaxRegistration { Jurisdiction = "US-MI", AccountId = "UI-1", Rate = 2.7m, Categories = [IncomeTaxCategories.Unemployment] },
        ];

        Assert.Equal(27m, await TaxAsync(provider, request, "US-MI-SUI"));
        Assert.Equal("UI-1", IncomeTaxRulesProvider.RegistrationFor(unemployment, request.Registrations).AccountId);

        // Registrations that name no category: the first, as before.
        request.Registrations =
        [
            new IncomeTaxRegistration { Jurisdiction = "US-MI", AccountId = "A", Rate = 3.1m },
            new IncomeTaxRegistration { Jurisdiction = "US-MI", AccountId = "B", Rate = 2.7m },
        ];

        Assert.Equal(31m, await TaxAsync(provider, request, "US-MI-SUI"));
    }

    [Fact]
    public async Task FormW4_AddsTheNonresidentAlienAmount_ToTheYearsWages()
    {
        // The 2025 single table of Publication 15-T (Worksheet 1A): 0% to 6,400, then 10%, 12%, 22%...
        var single = Table("single-2025", (0m, 6400m, 0m), (6400m, 18325m, 0.10m), (18325m, 54875m, 0.12m), (54875m, 109750m, 0.22m), (109750m, null, 0.24m));
        var rule = new IncomeTaxRule
        {
            TaxCode = "US-FIT",
            Jurisdiction = "US",
            Method = UnitedStatesIncomeTax.FormW4Method,
            Parameters = new(StringComparer.Ordinal) { [UnitedStatesIncomeTax.Adjustment] = 8600m, [UnitedStatesIncomeTax.NonresidentAlienAddition] = 15000m },
        };

        async Task<decimal> WithholdAsync(params (string Name, string Value)[] values)
            => Math.Round(await new UnitedStatesFormW4WithholdingMethod().CalculateAsync(new IncomeTaxWithholdingContext
            {
                Rule = rule,
                Request = new IncomeTaxRequest(),
                Election = Election("US", values)[0],
                Income = 2000m,
                PeriodsPerYear = 26,
                ComputeTableAsync = (_, amount, _) => Task.FromResult(new ProgressiveTaxCalculationMethod().Compute(new TaxComputationRequest { TaxableBase = amount, Table = single }).TaxAmount),
            }, TestContext.Current.CancellationToken), 2, MidpointRounding.AwayFromZero);

        // 2,000 x 26 = 52,000 - 8,600 = 43,400: 1,192.50 + 12% of 25,075 = 4,201.50 a year, 161.60 a paycheck.
        Assert.Equal(161.60m, await WithholdAsync(("FormYear", "2020"), ("FilingStatus", "Single")));

        // 43,400 + 15,000 = 58,400, in the row from 54,875 (5,578.50 by then) at 22%: 6,354 a year, 244.38 a paycheck.
        Assert.Equal(244.38m, await WithholdAsync(("FormYear", "2020"), ("FilingStatus", "Single"), ("NonresidentAlien", "true")));
    }

    [Fact]
    public async Task Verification_IsClearedByAnyChange_AndKeptOnlyWhenTheValuesCarryIt()
    {
        var handler = new IncomeTaxRuleHandler(new TestClock(new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc)), new ServiceCollection().BuildServiceProvider(), new PassThroughStringLocalizer<IncomeTaxRuleHandler>());
        var rule = new IncomeTaxRule
        {
            TaxCode = "US-MI-SIT",
            Jurisdiction = "US-MI",
            Source = IncomeTaxRuleSources.FlatRate,
            Rate = 4.25m,
            VerifiedBy = "admin",
            VerifiedUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            VerificationSource = "Michigan Form 446 (2026)",
        };

        // The Verify action hands the values in: they are kept.
        await handler.UpdatingAsync(new UpdatingContext<IncomeTaxRule>(rule, new JsonObject
        {
            [nameof(IncomeTaxRule.VerifiedBy)] = "checker",
            [nameof(IncomeTaxRule.VerifiedUtc)] = "2026-01-05T10:00:00Z",
        }), TestContext.Current.CancellationToken);

        Assert.True(rule.IsVerified);
        Assert.Equal("checker", rule.VerifiedBy);

        // An edit (a page passes no values) clears the check.
        rule.Rate = 4.05m;

        await handler.UpdatingAsync(new UpdatingContext<IncomeTaxRule>(rule, new JsonObject()), TestContext.Current.CancellationToken);

        Assert.False(rule.IsVerified);
        Assert.Null(rule.VerifiedBy);
        Assert.Null(rule.VerificationSource);
    }

    [Fact]
    public void StateCertificate_OwnsItsTablesParametersAndDeductedTaxes_AndDropsTheRest()
    {
        var rule = new IncomeTaxRule
        {
            Source = UnitedStatesIncomeTax.StateCertificateMethod,
            Rate = 5m,
            DeductedTaxCodes = ["US-SS-EE"],
            Tables = new(StringComparer.Ordinal) { [UnitedStatesStateCertificateParameters.Brackets] = "t" },
            ExemptionAllowance = 600m,
        };

        IncomeTaxRuleSources.ClearNotOwned(rule, [.. IncomeTaxRuleSources.WithholdingFields, nameof(IncomeTaxRule.DeductedTaxCodes)]);

        Assert.Null(rule.Rate);
        Assert.Null(rule.ExemptionAllowance);
        Assert.Equal(["US-SS-EE"], rule.DeductedTaxCodes);
        Assert.Equal("t", rule.Tables[UnitedStatesStateCertificateParameters.Brackets]);
        Assert.Contains(nameof(IncomeTaxRule.ExemptionAllowance), IncomeTaxRuleSources.FlatRateFields);
    }

    private static Task<IncomeTaxRulesProvider> ProviderAsync(InMemoryNamedCatalog<TaxTable> tables, IEnumerable<IncomeTaxRule> rules)
    {
        var manager = new Mock<IIncomeTaxRuleManager>();
        var list = rules.ToList();

        manager.Setup(candidate => candidate.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);

        return Task.FromResult(new IncomeTaxRulesProvider(
            manager.Object,
            new DefaultTaxCalculationMethodProvider([new PercentageTaxCalculationMethod(), new ProgressiveTaxCalculationMethod()]),
            tables,
            [new UnitedStatesFormW4WithholdingMethod(), new UnitedStatesStateCertificateWithholdingMethod()],
            new PassThroughStringLocalizer<IncomeTaxRulesProvider>()));
    }

    private static async Task<IncomeTaxRule> MichiganAsync(InMemoryNamedCatalog<TaxTable> tables)
        => await StateAsync(tables, "US-MI", [(0m, null, 0.0425m)], new()
        {
            [UnitedStatesStateCertificateParameters.FirstExemption] = 5800m,
            [UnitedStatesStateCertificateParameters.AdditionalExemption] = 5800m,
        });

    private static async Task<IncomeTaxRule> MassachusettsAsync(InMemoryNamedCatalog<TaxTable> tables)
    {
        var rule = await StateAsync(tables, "US-MA", [(0m, 1083150m, 0.05m), (1083150m, null, 0.09m)], new()
        {
            [UnitedStatesStateCertificateParameters.NoTaxThreshold] = 8000m,
            [UnitedStatesStateCertificateParameters.HeadOfHouseholdDeduction] = 2400m,
            [UnitedStatesStateCertificateParameters.FirstExemption] = 4400m,
            [UnitedStatesStateCertificateParameters.AdditionalExemption] = 1000m,
            [UnitedStatesStateCertificateParameters.SpecialExemption] = 2200m,
            [UnitedStatesStateCertificateParameters.DeductedTaxesLimit] = 2000m,
        });

        rule.DeductedTaxCodes = ["US-SS-EE", "US-MED-EE", "US-MED-ADD"];

        return rule;
    }

    private static async Task<IncomeTaxRule> VirginiaAsync(InMemoryNamedCatalog<TaxTable> tables)
        => await StateAsync(tables, "US-VA", [(0m, 3000m, 0.02m), (3000m, 5000m, 0.03m), (5000m, 17000m, 0.05m), (17000m, null, 0.0575m)], new()
        {
            [UnitedStatesStateCertificateParameters.StandardDeduction] = 8750m,
            [UnitedStatesStateCertificateParameters.FirstExemption] = 930m,
            [UnitedStatesStateCertificateParameters.AdditionalExemption] = 930m,
            [UnitedStatesStateCertificateParameters.SpecialExemption] = 800m,
        });

    private static async Task<IncomeTaxRule> StateAsync(InMemoryNamedCatalog<TaxTable> tables, string state, (decimal From, decimal? To, decimal Rate)[] brackets, Dictionary<string, decimal> parameters)
    {
        var table = Table($"{state}-2025", brackets);

        await tables.CreateAsync(table, TestContext.Current.CancellationToken);

        return new IncomeTaxRule
        {
            ItemId = state,
            TaxCode = $"{state}-SIT",
            Name = $"{state} income tax",
            Jurisdiction = state,
            Category = IncomeTaxCategories.IncomeTax,
            Source = UnitedStatesIncomeTax.StateCertificateMethod,
            Method = UnitedStatesIncomeTax.StateCertificateMethod,
            UsesElections = true,
            Tables = new(StringComparer.Ordinal) { [UnitedStatesStateCertificateParameters.Brackets] = table.ItemId },
            Parameters = new(parameters, StringComparer.Ordinal),
            EffectiveFrom = new DateOnly(2025, 1, 1),
        };
    }

    private static TaxTable Table(string id, params (decimal From, decimal? To, decimal Rate)[] rows)
        => new()
        {
            ItemId = id,
            Name = id,
            Rows = [.. rows.Select(row => new TaxTableRow { Minimum = row.From, Maximum = row.To, Rate = row.Rate })],
        };

    // Social Security, Medicare and Additional Medicare, the employee's, as the federal recipe has them.
    private static IncomeTaxRule[] Federal()
        =>
        [
            FlatRate("US-SS-EE", "US", IncomeTaxCategories.SocialInsurance, 6.2m, wageBase: 176100m),
            FlatRate("US-MED-EE", "US", IncomeTaxCategories.SocialInsurance, 1.45m),
            FlatRate("US-MED-ADD", "US", IncomeTaxCategories.SocialInsurance, 0.9m, threshold: 200000m),
        ];

    // Massachusetts' paid family and medical leave: 0.46% from the employee, 0.42% from an employer of 25 or more.
    private static IncomeTaxRule[] PaidLeave()
        =>
        [
            FlatRate("US-MA-PFML-EE", "US-MA", IncomeTaxCategories.SocialInsurance, 0.46m, wageBase: 176100m),
            FlatRate("US-MA-PFML-ER", "US-MA", IncomeTaxCategories.SocialInsurance, 0.42m, wageBase: 176100m, payer: IncomeTaxPayer.Employer),
        ];

    // Detroit (2.4% / 1.2%) and Grand Rapids (1.5% / 0.75%), 600 an exemption, of which only one city applies.
    private static IncomeTaxRule[] Cities()
        =>
        [
            City("US-MI-DETROIT-R", "US-MI-DETROIT", 2.4m, IncomeTaxResidency.Resident, 0),
            City("US-MI-DETROIT-NR", "US-MI-DETROIT", 1.2m, IncomeTaxResidency.Nonresident, 1),
            City("US-MI-GRANDRAPIDS-R", "US-MI-GRANDRAPIDS", 1.5m, IncomeTaxResidency.Resident, 0),
            City("US-MI-GRANDRAPIDS-NR", "US-MI-GRANDRAPIDS", 0.75m, IncomeTaxResidency.Nonresident, 1),
        ];

    private static IncomeTaxRule City(string taxCode, string jurisdiction, decimal rate, IncomeTaxResidency residency, int priority)
    {
        var rule = FlatRate(taxCode, jurisdiction, IncomeTaxCategories.IncomeTax, rate);

        rule.Residency = residency;
        rule.ExclusiveGroup = "US-MI-CITY";
        rule.GroupPriority = priority;
        rule.ElectionsFrom = "US-MI";
        rule.ExemptionAllowance = 600m;

        return rule;
    }

    private static IncomeTaxRule FlatRate(string taxCode, string jurisdiction, string category, decimal rate, decimal? wageBase = null, decimal? threshold = null, IncomeTaxPayer payer = IncomeTaxPayer.Employee)
        => new()
        {
            ItemId = taxCode,
            TaxCode = taxCode,
            Name = taxCode,
            Jurisdiction = jurisdiction,
            Category = category,
            Payer = payer,
            Source = IncomeTaxRuleSources.FlatRate,
            Method = TaxCalculationMethodNames.Percentage,
            Rate = rate,
            WageBase = wageBase,
            WageThreshold = threshold,
            EffectiveFrom = new DateOnly(2025, 1, 1),
        };

    private static IncomeTaxRequest Request(decimal wages, int periods, string state, bool federal = false, IList<IncomeTaxElection> elections = null)
        => new()
        {
            RegisteredJurisdictions = federal ? ["US", state] : [state],
            HomeJurisdictions = ["US", state],
            PayDate = _payDate,
            PeriodsPerYear = periods,
            Elections = elections ?? [],
            Income = new Dictionary<string, decimal>
            {
                [IncomeTaxCategories.IncomeTax] = wages,
                [IncomeTaxCategories.SocialInsurance] = wages,
                [IncomeTaxCategories.Unemployment] = wages,
            },
        };

    private static IncomeTaxRequest Michigan(decimal wages, IList<string> homeCities, IList<string> workCities, IList<IncomeTaxElection> elections)
    {
        var request = Request(wages, 26, "US-MI", elections: elections);

        request.RegisteredJurisdictions = ["US-MI", "US-MI-DETROIT", "US-MI-GRANDRAPIDS"];
        request.HomeJurisdictions = ["US", "US-MI", .. homeCities];
        request.WorkJurisdictions = workCities.Count == 0 ? [] : ["US-MI", .. workCities];

        return request;
    }

    private static List<IncomeTaxElection> Election(string jurisdiction, params (string Name, string Value)[] values)
        => [new IncomeTaxElection { Jurisdiction = jurisdiction, Values = values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal) }];

    private static async Task<IReadOnlyList<IncomeTaxLine>> LinesAsync(IncomeTaxRulesProvider provider, IncomeTaxRequest request)
    {
        var result = await provider.CalculateAsync(request, TestContext.Current.CancellationToken);

        Assert.Empty(result.Warnings);

        return [.. result.Lines];
    }

    private static async Task<decimal> TaxAsync(IncomeTaxRulesProvider provider, IncomeTaxRequest request, string taxCode)
        => Amount(await LinesAsync(provider, request), taxCode);

    // A line's amount in cents, as a pay statement keeps it; zero when there is no line.
    private static decimal Amount(IReadOnlyList<IncomeTaxLine> lines, string taxCode)
        => Math.Round(lines.Where(line => line.TaxCode == taxCode).Sum(line => line.Amount), 2, MidpointRounding.AwayFromZero);
}
