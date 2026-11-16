using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The Canada pack: the shipped 2026 recipe imported as it is, and source deductions worked out from it. Every expected
/// amount comes from a separate worksheet of the published formulas (CRA T4127, 122nd and 123rd editions, and Revenu
/// Québec TP-1015.F 2026-01), written apart from the code: CPP C = 5.95% × (PI − 3,500 ÷ P cut to cents) within 4,230.45;
/// CPP2 = 4% of the year's pensionable earnings between 74,600 and 85,000 within 416; EI 1.63% within 1,123.07 (1.30% and
/// 895.70 in Quebec); A = P × (I − F5A); T3 = R × A − K − K1 − K2 − K4; T2 = T4 + V1 + V2 − S; a bonus by the year's tax
/// with and without it; and in Quebec I = P × (G − H − CSA), Y = T × I − K − 0.14 × E.
/// </summary>
public sealed class CanadaIncomeTaxTests
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static TheoryData<string, string, int, string, decimal, decimal, Dictionary<string, decimal>> Cases => new()
    {
        // Ontario, every two weeks, no TD1 (the basic amounts), with the surtax, health premium and tax reduction in play.
        { "Ontario, 2,500 biweekly", "CA-ON", 26, "2026-03-13", 2500m, 0m, Expect(cpp: 140.74m, ei: 40.75m, federal: 242.58m, provincial: 131.46m, province: "CA-ON") },
        { "Ontario, 6,000 biweekly", "CA-ON", 26, "2026-03-13", 6000m, 0m, Expect(cpp: 348.99m, ei: 97.80m, federal: 1029.20m, provincial: 602.06m, province: "CA-ON") },

        // A weekly pay: the CPP basic exemption is 3,500 ÷ 52 = 67.3077, cut to 67.30.
        { "Ontario, 1,000 weekly", "CA-ON", 52, "2026-03-13", 1000m, 0m, Expect(cpp: 55.50m, ei: 16.30m, federal: 81.61m, provincial: 45.80m, province: "CA-ON") },

        // A bonus with the pay: CPP and EI on all of it, tax by the year's tax with and without the bonus.
        { "Ontario, 2,500 and a 5,000 bonus", "CA-ON", 26, "2026-03-13", 2500m, 5000m, Expect(cpp: 438.24m, ei: 122.25m, federal: 1256.70m, provincial: 584.16m, province: "CA-ON") },

        // British Columbia changes on July 1 (123rd edition): a lowest rate of 6.14% and a tax reduction of 805.
        { "British Columbia, 3,000 biweekly in March", "CA-BC", 26, "2026-03-13", 3000m, 0m, Expect(cpp: 170.49m, ei: 48.90m, federal: 341.15m, provincial: 142.88m, province: "CA-BC") },
        { "British Columbia, 3,000 biweekly in August", "CA-BC", 26, "2026-08-14", 3000m, 0m, Expect(cpp: 170.49m, ei: 48.90m, federal: 341.15m, provincial: 156.39m, province: "CA-BC") },
        { "British Columbia, 1,200 biweekly: the tax reduction", "CA-BC", 26, "2026-03-13", 1200m, 0m, Expect(cpp: 63.39m, ei: 19.56m, federal: 59.72m, provincial: 16.02m, province: "CA-BC") },

        // Alberta, monthly: the supplemental credit (K5P) applies above 4,896 of credits.
        { "Alberta, 8,000 monthly", "CA-AB", 12, "2026-03-31", 8000m, 0m, Expect(cpp: 458.65m, ei: 130.40m, federal: 1043.58m, provincial: 507.55m, province: "CA-AB") },
        { "Alberta, 3,000 monthly", "CA-AB", 12, "2026-03-31", 3000m, 0m, Expect(cpp: 161.15m, ei: 48.90m, federal: 181.14m, provincial: 71.40m, province: "CA-AB") },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OutsideQuebec_FollowsT4127(string description, string province, int periods, string payDate, decimal pay, decimal bonus, Dictionary<string, decimal> expected)
    {
        Assert.NotNull(description);

        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(province, periods, DateOnly.Parse(payDate, System.Globalization.CultureInfo.InvariantCulture), pay, bonus);

        Assert.Equal(expected, await AmountsAsync(harness, request));
    }

    [Fact]
    public async Task Quebec_HasQppQpipAndTheReducedEiRate_QuebecTaxByTP1015_AndTheFederalAbatement()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var amounts = await AmountsAsync(harness, Request("CA-QC", 26, new DateOnly(2026, 3, 13), 2500m, 0m));

        // QPP 6.30% × (2,500 − 134.61); EI 1.30%; QPIP 0.43%, the employer's 1.4 times; the employer's health services
        // fund at the lowest rate, 1.65%, with no rate on its registration; no CPP, no Ontario.
        Assert.Equal(new Dictionary<string, decimal>
        {
            ["CA-EI-QC"] = 32.50m,
            ["CA-EI-QC-ER"] = 45.50m,
            ["CA-FIT"] = 201.29m,
            ["CA-QC-HSF"] = 41.25m,
            ["CA-QC-PIT"] = 253.36m,
            ["CA-QC-QPIP"] = 10.75m,
            ["CA-QC-QPIP-ER"] = 15.05m,
            ["CA-QC-QPP"] = 149.02m,
            ["CA-QC-QPP-ER"] = 149.02m,
        }, amounts);
    }

    [Fact]
    public async Task Quebec_TheHealthServicesFund_IsAtTheEmployersOwnRate()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request("CA-QC", 26, new DateOnly(2026, 3, 13), 2500m, 1000m);

        // The rate Revenu Québec gave the employer for its total payroll, on its CA-QC registration: 2,500 + 1,000 at 2.31%.
        request.Registrations = [new IncomeTaxRegistration { Jurisdiction = "CA-QC", AccountId = "1234567890TQ0001", Rate = 2.31m }];

        Assert.Equal(80.85m, (await AmountsAsync(harness, request))["CA-QC-HSF"]);

        // An employer with no Quebec registration has none.
        request.Registrations = [];
        request.RegisteredJurisdictions = ["CA"];

        Assert.False((await AmountsAsync(harness, request)).ContainsKey("CA-QC-HSF"));
    }

    [Fact]
    public async Task Quebec_ALumpSum_IsWithheldAtTheMarginalRate_AndTheFederalBonusMethod()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var amounts = await AmountsAsync(harness, Request("CA-QC", 26, new DateOnly(2026, 3, 13), 2500m, 3000m));

        Assert.Equal((338.02m, 71.50m, 23.65m, 709.23m, 817.66m), (amounts["CA-QC-QPP"], amounts["CA-EI-QC"], amounts["CA-QC-QPIP"], amounts["CA-FIT"], amounts["CA-QC-PIT"]));
    }

    [Theory]
    [InlineData("CA-ON", "CA-ON-PIT")]
    [InlineData("CA-QC", "CA-QC-PIT")]
    public async Task ABonusPaidAlone_IsWithheldOnTopOfAYearOfRegularPay_NotOfNothing(string province, string provincialCode)
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var payDate = new DateOnly(2026, 3, 13);

        // The bonus method's share of a 5,000 bonus paid with 2,500 of pay: what it adds to the pay's tax.
        var withPay = await AmountsAsync(harness, Request(province, 26, payDate, 2500m, 5000m));
        var payAlone = await AmountsAsync(harness, Request(province, 26, payDate, 2500m, 0m));

        // The same bonus paid alone, before (annualizing this payment's regular pay of nothing) and after (a period of
        // the person's regular pay of 2,500 sent with it).
        var unlinked = await AmountsAsync(harness, Request(province, 26, payDate, 0m, 5000m));
        var request = Request(province, 26, payDate, 0m, 5000m);

        request.RegularIncomePerPeriod[IncomeTaxCategories.IncomeTax] = 2500m;

        var alone = await AmountsAsync(harness, request);

        foreach (var code in new[] { "CA-FIT", provincialCode })
        {
            var share = withPay[code] - payAlone[code];

            // A year of 65,000 puts the bonus in a higher bracket than a year of nothing does.
            Assert.True(alone[code] > unlinked.GetValueOrDefault(code) + 100m, $"{code}: {alone[code]} vs {unlinked.GetValueOrDefault(code)}");

            // What is left between the two is the pay's own enhancement deduction and credits, which a bonus paid alone
            // has none of: within 3%.
            Assert.InRange(alone[code], share * 0.97m, share * 1.03m);
        }
    }

    [Fact]
    public async Task WithNoProvinceOfEmployment_NoProvinceTaxesAPerson_WhereverTheyLive()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        // A person living in Quebec whose place of work isn't known: no Quebec tax, QPP or QPIP from where they live.
        var request = Request("CA-QC", 26, new DateOnly(2026, 3, 13), 2500m, 0m);

        request.WorkJurisdictions = [];

        var result = await harness.RunAsync(services => services.GetServices<IIncomeTaxProvider>().Single().CalculateAsync(request), commit: false);
        var codes = result.Lines.Where(line => line.Amount != 0).Select(line => line.TaxCode).ToList();

        Assert.DoesNotContain(codes, code => code.StartsWith("CA-QC-", StringComparison.Ordinal));
        Assert.Contains(CanadaIncomeTax.TaxCodes.Cpp, codes);
        Assert.Contains("No place of work in CA", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMaximums_AreReachedDuringTheYear_AndCpp2StartsAboveTheFirstCeiling()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request("CA-ON", 26, new DateOnly(2026, 11, 20), 5000m, 0m);

        // 4,200.00 of CPP and 1,120.00 of EI already this year, on 72,000 of pensionable earnings.
        request.TaxesToDate["CA-CPP"] = new IncomeTaxToDate { YearTax = 4200m };
        request.TaxesToDate["CA-CPP-ER"] = new IncomeTaxToDate { YearTax = 4200m };
        request.TaxesToDate["CA-EI"] = new IncomeTaxToDate { YearTax = 1120m };
        request.TaxesToDate["CA-EI-ER"] = new IncomeTaxToDate { YearTax = 1568m };
        request.YearIncome[IncomeTaxCategories.SocialInsurance] = 72000m;

        var amounts = await AmountsAsync(harness, request);

        // CPP up to 4,230.45; CPP2 on 77,000 − 74,600; EI up to 1,123.07; the employer's EI within 1,572.30.
        Assert.Equal((30.45m, 30.45m, 96.00m, 96.00m, 3.07m, 4.30m, 779.18m, 429.72m),
            (amounts["CA-CPP"], amounts["CA-CPP-ER"], amounts["CA-CPP2"], amounts["CA-CPP2-ER"], amounts["CA-EI"], amounts["CA-EI-ER"], amounts["CA-FIT"], amounts["CA-ON-PIT"]));

        // Once at the maximums nothing more is taken.
        request.TaxesToDate["CA-CPP"] = new IncomeTaxToDate { YearTax = 4230.45m };
        request.TaxesToDate["CA-EI"] = new IncomeTaxToDate { YearTax = 1123.07m };

        var after = await AmountsAsync(harness, request);

        Assert.False(after.ContainsKey("CA-CPP"));
        Assert.False(after.ContainsKey("CA-EI"));
    }

    [Fact]
    public async Task APersonLivingInQuebecAndWorkingInOntario_IsTaxedByTheProvinceOfEmployment()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request("CA-ON", 26, new DateOnly(2026, 3, 13), 2500m, 0m);

        request.HomeJurisdictions = ["CA", "CA-QC"];

        // As anyone working in Ontario: CPP, EI at the full rate, Ontario tax; nothing of Quebec's.
        Assert.Equal(Expect(cpp: 140.74m, ei: 40.75m, federal: 242.58m, provincial: 131.46m, province: "CA-ON"), await AmountsAsync(harness, request));
    }

    [Fact]
    public async Task ATD1AboveTheBasicAmount_LowersFederalAndProvincialTax()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request("CA-ON", 26, new DateOnly(2026, 3, 13), 2500m, 0m);

        request.Elections =
        [
            new IncomeTaxElection { Jurisdiction = "CA", Values = new() { [CanadaIncomeTax.Fields.ClaimAmount] = "25000" } },
            new IncomeTaxElection { Jurisdiction = "CA-ON", Values = new() { [CanadaIncomeTax.Fields.ClaimAmount] = "20000" } },
        ];

        var amounts = await AmountsAsync(harness, request);

        Assert.Equal((196.55m, 117.85m), (amounts["CA-FIT"], amounts["CA-ON-PIT"]));
    }

    [Fact]
    public async Task AnEmployerNotRegisteredInQuebec_HasNoQuebecDeductions_AndAProvinceIsCollectedByTheCra()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request("CA-QC", 26, new DateOnly(2026, 3, 13), 2500m, 0m);

        request.RegisteredJurisdictions = ["CA"];

        var amounts = await AmountsAsync(harness, request);

        // The federal tax and the Quebec EI rate are the CRA's; QPP, QPIP and Quebec tax need a Revenu Québec number.
        Assert.Contains("CA-FIT", amounts.Keys);
        Assert.Contains("CA-EI-QC", amounts.Keys);
        Assert.DoesNotContain(amounts.Keys, code => code.StartsWith("CA-QC-", StringComparison.Ordinal));
        Assert.DoesNotContain("CA-CPP", amounts.Keys);
    }

    [Fact]
    public void Recipe_CoversEveryProvinceAndTerritory_AndTheTablesMatchTheCrasConstants()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(RecipePath()))!.AsObject();
        var steps = recipe["steps"]!.AsArray();
        var rules = steps.Single(step => step!["name"]!.GetValue<string>() == TaxationConstants.IncomeTaxRulesRecipeStepName)!["Rules"]!.AsArray();
        var tables = steps.Single(step => step!["name"]!.GetValue<string>() == "TaxTable")!["TaxTables"].Deserialize<List<TaxTable>>(_options);
        var provinces = rules.Select(rule => rule!["Jurisdiction"]!.GetValue<string>()).Where(jurisdiction => jurisdiction.StartsWith("CA-", StringComparison.Ordinal)).ToHashSet();

        Assert.Equal(["CA-AB", "CA-BC", "CA-MB", "CA-NB", "CA-NL", "CA-NS", "CA-NT", "CA-NU", "CA-ON", "CA-PE", "CA-QC", "CA-SK", "CA-YT"], provinces.Order());

        // The bands' fixed amounts make the table give R × A − K with the CRA's rounded constants.
        var federal = tables.Single(table => table.Name.StartsWith("Canada federal", StringComparison.Ordinal));
        var progressive = new ProgressiveTaxCalculationMethod();

        Assert.Equal(0.205m * 64385m - 3804m, progressive.Compute(new TaxComputationRequest { TaxableBase = 64385m, Table = federal }).TaxAmount);
        Assert.Equal(0.33m * 300000m - 26024m, progressive.Compute(new TaxComputationRequest { TaxableBase = 300000m, Table = federal }).TaxAmount);
    }

    private static Dictionary<string, decimal> Expect(decimal cpp, decimal ei, decimal federal, decimal provincial, string province)
        => new()
        {
            ["CA-CPP"] = cpp,
            ["CA-CPP-ER"] = cpp,
            ["CA-EI"] = ei,
            ["CA-EI-ER"] = Math.Round(ei * 1.4m, 2, MidpointRounding.AwayFromZero),
            ["CA-FIT"] = federal,
            [province + "-PIT"] = provincial,
        };

    private static IncomeTaxRequest Request(string province, int periods, DateOnly payDate, decimal pay, decimal bonus)
    {
        var request = new IncomeTaxRequest
        {
            RegisteredJurisdictions = ["CA", "CA-QC"],
            WorkJurisdictions = [province],
            HomeJurisdictions = [province],
            PayDate = payDate,
            PeriodsPerYear = periods,
            Currency = "CAD",
        };

        foreach (var category in new[] { IncomeTaxCategories.IncomeTax, IncomeTaxCategories.SocialInsurance, CanadaIncomeTax.Categories.InsurableEarnings })
        {
            request.Income[category] = pay;
            request.SupplementalIncome[category] = bonus;
        }

        return request;
    }

    [Fact]
    public async Task RecipeRules_WithoutASource_GetOneFromWhatTheyHold_AndASourceSetsItsMethod()
    {
        await using var harness = await CreateHarnessAsync();

        // Rules written before rules had a source; and one that names its source and no method.
        Assert.Empty(await ImportRecipeAsync(harness, rules =>
        {
            foreach (var rule in rules.Select(node => node!.AsObject()))
            {
                rule.Remove("Source");

                if (rule["TaxCode"]!.GetValue<string>() == "CA-QC-HSF")
                {
                    rule.Remove("Method");
                    rule["Source"] = IncomeTaxRuleSources.FlatRate;
                }
            }
        }));

        var stored = await harness.RunAsync(async services => (await services.GetRequiredService<IIncomeTaxRuleManager>().ListAsync())
            .GroupBy(rule => rule.TaxCode)
            .ToDictionary(group => group.Key, group => (group.First().Source, group.First().Method), StringComparer.Ordinal), commit: false);

        Assert.Equal((CanadaIncomeTax.FederalMethod, CanadaIncomeTax.FederalMethod), stored["CA-FIT"]);
        Assert.Equal((CanadaIncomeTax.ProvincialMethod, CanadaIncomeTax.ProvincialMethod), stored["CA-ON-PIT"]);
        Assert.Equal((CanadaIncomeTax.QuebecMethod, CanadaIncomeTax.QuebecMethod), stored["CA-QC-PIT"]);
        Assert.Equal((IncomeTaxRuleSources.FlatRate, TaxCalculationMethodNames.Percentage), stored["CA-CPP"]);
        Assert.Equal((IncomeTaxRuleSources.ShareOfTax, TaxCalculationMethodNames.Percentage), stored["CA-EI-ER"]);
        Assert.Equal((IncomeTaxRuleSources.FlatRate, TaxCalculationMethodNames.Percentage), stored["CA-QC-HSF"]);
    }

    [Fact]
    public async Task RecipeRules_KeepOnlyTheirSourcesFields_AndAShareOutsideItsSourceIsRefused()
    {
        await using var harness = await CreateHarnessAsync();

        // A federal withholding rule with a yearly maximum it doesn't show, and a flat rate that names a share.
        var errors = await ImportRecipeAsync(harness, rules =>
        {
            foreach (var rule in rules.Select(node => node!.AsObject()))
            {
                switch (rule["TaxCode"]!.GetValue<string>())
                {
                    case "CA-FIT":
                        rule["YearlyMaximum"] = 1;
                        break;
                    case "CA-QC-HSF":
                        rule["ShareOfTaxCode"] = "CA-EI";
                        rule["ShareMultiplier"] = 1;
                        break;
                }
            }
        });

        var stored = await harness.RunAsync(async services => (await services.GetRequiredService<IIncomeTaxRuleManager>().ListAsync())
            .Where(rule => rule.TaxCode is "CA-FIT" or "CA-QC-HSF")
            .ToList(), commit: false);

        // The hidden maximum is gone, so it can't cap the withholding; the share is refused, not silently dropped.
        Assert.Null(Assert.Single(stored, rule => rule.TaxCode == "CA-FIT").YearlyMaximum);
        Assert.DoesNotContain(stored, rule => rule.TaxCode == "CA-QC-HSF");
        Assert.Contains(errors, error => error.Contains("Only a share of another tax", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AShareWithoutItsTaxCode_IsRefusedForThatField()
    {
        await using var harness = await CreateHarnessAsync();

        var errors = await ImportRecipeAsync(harness, rules =>
        {
            foreach (var rule in rules.Select(node => node!.AsObject()).Where(rule => rule["TaxCode"]!.GetValue<string>() == "CA-EI-ER"))
            {
                rule.Remove("ShareOfTaxCode");
            }
        });

        Assert.Contains(errors, error => error.Contains("Enter the code of the tax this is a share of.", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("rate", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Dictionary<string, decimal>> AmountsAsync(CalendarTestHarness harness, IncomeTaxRequest request)
        => await harness.RunAsync(async services =>
        {
            var result = await services.GetServices<IIncomeTaxProvider>().Single().CalculateAsync(request);

            Assert.Empty(result.Warnings);

            return result.Lines
                .Where(line => Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero) != 0)
                .ToDictionary(line => line.TaxCode, line => Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero), StringComparer.Ordinal);
        }, commit: false);

    // Imports the shipped recipe: its tax tables into the catalog, its rules through the rules step (and their handler).
    private static async Task<IReadOnlyList<string>> ImportRecipeAsync(CalendarTestHarness harness, Action<JsonArray> editRules = null)
    {
        var recipe = JsonNode.Parse(await File.ReadAllTextAsync(RecipePath(), TestContext.Current.CancellationToken))!.AsObject();
        var steps = recipe["steps"]!.AsArray();

        Assert.Equal([TaxationConstants.Feature.IncomeTax, TaxationConstants.Feature.IncomeTaxCanada], steps.Single(step => step!["name"]!.GetValue<string>() == "Feature")!["enable"]!.AsArray().Select(feature => feature!.GetValue<string>()));

        var tables = steps.Single(step => step!["name"]!.GetValue<string>() == "TaxTable")!["TaxTables"].Deserialize<List<TaxTable>>(_options);
        var rules = steps.Single(step => step!["name"]!.GetValue<string>() == TaxationConstants.IncomeTaxRulesRecipeStepName)!.AsObject();

        editRules?.Invoke(rules["Rules"]!.AsArray());

        return await harness.RunAsync(async services =>
        {
            var catalog = services.GetRequiredService<INamedCatalog<TaxTable>>();

            foreach (var table in tables)
            {
                await catalog.CreateAsync(table);
            }

            var context = new RecipeExecutionContext { Name = TaxationConstants.IncomeTaxRulesRecipeStepName, Step = rules.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<IncomeTaxRulesRecipeStep>(services).ExecuteAsync(context);

            return (IReadOnlyList<string>)[.. context.Errors];
        });
    }

    private static string RecipePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The repository root was not found."), "src", "Modules", "CrestApps.OrchardCore.Taxation", "Recipes", "income-tax-canada-2026.recipe.json");
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddIncomeTaxCore();
                services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<IncomeTaxRule>, IncomeTaxRuleHandler>());
                services.AddSingleton<INamedCatalog<TaxTable>>(new InMemoryNamedCatalog<TaxTable>());
                services.AddCanadaIncomeTax();
            },
            IncomeTaxRuleIndexSchema.CreateAsync,
            [new IncomeTaxRuleIndexProvider()],
            [TaxationConstants.IncomeTaxCollectionName]);
}
