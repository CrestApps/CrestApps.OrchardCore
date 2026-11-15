using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The United States pack: the shipped 2026 federal recipe imported as it is, and withholding worked out from it by
/// the Form W-4 method (IRS Publication 15-T, Worksheet 1A), the Social Security wage base, the Additional Medicare
/// threshold, the supplemental rates and a second pay in a period. Every expected amount is worked by hand below from
/// the 2026 annual tables.
/// </summary>
public sealed class UnitedStatesPayrollTests
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static TheoryData<string, string, decimal> FormW4Cases()
        => new()
        {
            // 2000 x 26 = 52000 - 8600 = 43400; single: 1240 + 12% of 23500 = 4060; / 26 = 156.15.
            { "Single, no adjustments", """{ "FormYear": "2020", "FilingStatus": "Single" }""", 156.15m },

            // No election: withheld as single with no adjustments.
            { "No election", null, 156.15m },

            // 3000 x 26 = 78000, Step 2 checked so no line 1g; married, Step 2: 5800 + 22% of 11500 = 8330;
            // / 26 = 320.3846 less 4000 / 26 = 153.8462: 166.54.
            { "Married, Step 2, dependents", """{ "FormYear": "2020", "FilingStatus": "MarriedFilingJointly", "MultipleJobs": "true", "Dependents": "4000", "Wages": "3000" }""", 166.54m },

            // 1000 x 52 = 52000 + 5200 - 2600 - 8600 = 46000; head of household: 1770 + 12% of 12750 = 3300;
            // / 52 = 63.4615 + 25 extra: 88.46.
            { "Head of household, other income, deductions, extra", """{ "FormYear": "2020", "FilingStatus": "HeadOfHousehold", "OtherIncome": "5200", "Deductions": "2600", "ExtraWithholding": "25", "Wages": "1000", "Weekly": "true" }""", 88.46m },

            // A 2019 form: 2500 x 26 = 65000 - 3 x 4300 = 52100; married: 2480 + 12% of 8000 = 3440; / 26 = 132.31.
            { "2019 form, married, 3 allowances", """{ "FormYear": "2019", "FilingStatus": "MarriedFilingJointly", "Allowances": "3", "Wages": "2500" }""", 132.31m },
        };

    [Theory]
    [MemberData(nameof(FormW4Cases))]
    public async Task FederalIncomeTax_FollowsWorksheet1A_OnTheShippedTables(string description, string election, decimal expected)
    {
        Assert.NotNull(description);

        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var values = election is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(election);
        var wages = values?.GetValueOrDefault("Wages") is { } text ? decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture) : 2000m;
        var request = Request(wages, periods: values?.GetValueOrDefault("Weekly") == "true" ? 52 : 26);

        if (values is not null)
        {
            request.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1), Values = values }];
        }

        var lines = await CalculateAsync(harness, request);

        Assert.Equal(expected, Math.Round(Assert.Single(lines, line => line.TaxCode == "US-FIT").Amount, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task Exempt_WithholdsNoIncomeTax_ButSocialSecurityAndMedicareStill()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(2000m);

        request.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1), Values = new() { ["FormYear"] = "2020", ["Exempt"] = "true" } }];

        var lines = await CalculateAsync(harness, request);

        Assert.DoesNotContain(lines, line => line.TaxCode == "US-FIT");
        Assert.Equal(124.00m, lines.Single(line => line.TaxCode == "US-SS-EE").Amount);
        Assert.Equal(29.00m, lines.Single(line => line.TaxCode == "US-MED-EE").Amount);
        Assert.Equal(12.00m, lines.Single(line => line.TaxCode == "US-FUTA").Amount);
    }

    [Fact]
    public async Task WageBaseAndThreshold_TaxOnlyTheWagesLeftUnderOrAboveThem()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(10000m);

        // 180000 of Social Security wages and the 7000 of FUTA wages already taxed; 195000 of wages paid this year.
        request.Accumulators.Totals[PayAccumulators.TaxKey("US-SS-EE")] = new PayAccumulator { YearTaxableWages = 180000m };
        request.Accumulators.Totals[PayAccumulators.TaxKey("US-SS-ER")] = new PayAccumulator { YearTaxableWages = 180000m };
        request.Accumulators.Totals[PayAccumulators.TaxKey("US-FUTA")] = new PayAccumulator { YearTaxableWages = 7000m };
        request.YearWages[PayrollTaxCategories.SocialInsurance] = 195000m;

        var lines = await CalculateAsync(harness, request);

        // 184500 - 180000 = 4500 at 6.2% = 279.00, each; Medicare on all 10000; Additional Medicare on 205000 - 200000.
        Assert.Equal((279.00m, 4500m), Taxed(lines, "US-SS-EE"));
        Assert.Equal((279.00m, 4500m), Taxed(lines, "US-SS-ER"));
        Assert.Equal((145.00m, 10000m), Taxed(lines, "US-MED-EE"));
        Assert.Equal((45.00m, 5000m), Taxed(lines, "US-MED-ADD"));
        Assert.DoesNotContain(lines, line => line.TaxCode == "US-FUTA");

        // Below the threshold no Additional Medicare line at all.
        request.YearWages[PayrollTaxCategories.SocialInsurance] = 20000m;

        Assert.DoesNotContain(await CalculateAsync(harness, request), line => line.TaxCode == "US-MED-ADD");
    }

    [Fact]
    public async Task SupplementalPay_IsWithheldAt22Percent_And37PercentOverAMillionInTheYear_EvenWhenExempt()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(0m);

        request.SupplementalMethod = PayrollSupplementalMethod.Flat;
        request.SupplementalWages[PayrollTaxCategories.IncomeTax] = 5000m;
        request.YearSupplementalWages[PayrollTaxCategories.IncomeTax] = 998000m;

        // 2000 under the million at 22% = 440; 3000 over it at 37% = 1110.
        Assert.Equal((1550.00m, 5000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));

        request.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1), Values = new() { ["Exempt"] = "true" } }];

        Assert.Equal((1110.00m, 3000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));
    }

    [Fact]
    public async Task ABonusPaidOnItsOwn_ByTheAggregateMethod_IsWithheldOnThePeriodsTotal_WithoutExtraWithholdingAgain()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(1000m);

        // The regular pay of the period was 2000, withheld on already.
        request.Accumulators.Totals[PayAccumulators.TaxKey("US-FIT")] = new PayAccumulator { PeriodTaxableWages = 2000m, Period = 166.15m };
        request.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1), Values = new() { ["FormYear"] = "2020", ["FilingStatus"] = "Single", ["ExtraWithholding"] = "10" } }];

        // W(3000) = (78000 - 8600 = 69400: 5800 + 22% of 11500 = 8330) / 26 = 320.3846; W(2000) = 156.1538: 164.23.
        Assert.Equal((164.23m, 1000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));
    }

    [Fact]
    public async Task AFormW4Rule_NeedsEveryTable_AndATableItUsesIsKept()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var (missing, usages) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayrollTaxRuleManager>();
            var rule = (await manager.ListAsync()).Single(candidate => candidate.TaxCode == "US-FIT");

            var singleTableId = rule.Tables[UnitedStatesPayroll.FilingStatuses.Single];

            rule.ItemId = CrestApps.Core.UniqueId.GenerateId();
            rule.EffectiveFrom = new DateOnly(2027, 1, 1);
            rule.EffectiveTo = null;
            rule.Tables.Remove(UnitedStatesPayroll.FilingStatuses.HeadOfHousehold);

            var result = await manager.ValidateAsync(rule);
            var table = await services.GetRequiredService<INamedCatalog<TaxTable>>().FindByIdAsync(singleTableId);

            return (result, await services.GetRequiredService<PayrollTaxRuleTableUsage>().GetUsagesAsync(table));
        }, commit: false);

        Assert.False(missing.Succeeded);
        Assert.Contains(missing.Errors, error => error.ErrorMessage.Contains("Head of household", StringComparison.Ordinal));
        Assert.Contains(usages, usage => usage.Contains("US-FIT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recipe_GivesEveryTableAndRuleAValidUniqueId_SoTheImportKeepsTheirLinks()
    {
        // The import replaces an id that is not a UniqueId, and the rules' links to their tables would break.
        var recipe = JsonNode.Parse(await File.ReadAllTextAsync(RecipePath(), TestContext.Current.CancellationToken))!.AsObject();
        var ids = recipe["steps"]!.AsArray()
            .SelectMany(step => (step!["TaxTables"] ?? step["Rules"])?.AsArray() ?? [])
            .Select(entry => entry!["ItemId"]!.GetValue<string>())
            .ToList();

        Assert.Equal(13, ids.Count);
        Assert.All(ids, id => Assert.True(CrestApps.Core.UniqueId.IsValid(id), id));
    }

    [Fact]
    public void FormW4Fields_AreTheUSElection_AndOtherJurisdictionsKeepTheGenericOne()
    {
        var options = new PayrollElectionOptions();

        options.FieldSets.Add(GenericElectionFields.Create());
        options.FieldSets.Add(UnitedStatesElectionFields.Create());

        var federal = options.For("US");

        Assert.Equal(UnitedStatesPayroll.Jurisdiction, federal.Jurisdiction);
        Assert.Equal(["FormYear", "FilingStatus", "MultipleJobs", "Dependents", "OtherIncome", "Deductions", "ExtraWithholding", "Allowances", "Exempt"], federal.Fields.Select(field => field.Name));
        Assert.Equal(["Single", "MarriedFilingJointly", "HeadOfHousehold"], federal.Fields.Single(field => field.Name == "FilingStatus").Choices.Select(choice => choice.Value));
        Assert.Equal("*", options.For("US-CA").Jurisdiction);
    }

    [Fact]
    public void TaxRequest_CarriesTheYearsWagesFromThePayCodesTotals()
    {
        var regular = new PayCode { ItemId = "reg", Code = "REG", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Regular };
        var bonus = new PayCode { ItemId = "bonus", Code = "BONUS", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Bonus };

        var context = new PayCalculationContext
        {
            Codes = new Dictionary<string, PayCode> { ["reg"] = regular, ["bonus"] = bonus },
            PayDate = new DateOnly(2026, 11, 20),
        };

        context.Accumulators.Totals[PayAccumulators.CodeKey("reg")] = new PayAccumulator { Year = 190000m };
        context.Accumulators.Totals[PayAccumulators.CodeKey("bonus")] = new PayAccumulator { Year = 12000m };

        var categories = new PayrollTaxCategoryOptions();

        categories.Categories.Add(new PayrollTaxCategory(PayrollTaxCategories.IncomeTax, global::OrchardCore.Localization.LocalizationSource.Create<UnitedStatesPayrollTests>("Income tax")));

        var step = new TaxesPayCalculationStep([], Options.Create(categories), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());
        var request = step.Request(context);

        Assert.Equal(202000m, request.YearWages[PayrollTaxCategories.IncomeTax]);
        Assert.Equal(12000m, request.YearSupplementalWages[PayrollTaxCategories.IncomeTax]);
    }

    [Fact]
    public void Accumulators_SumThePeriodsTaxableWages()
    {
        var rows = new[]
        {
            new PayStatementLineIndex { LineKey = PayAccumulators.TaxKey("US-FIT"), PayDay = 20261120, PeriodStartDay = 20261101, PeriodEndDay = 20261114, AmountMinor = 15615, TaxableWagesMinor = 200000 },
            new PayStatementLineIndex { LineKey = PayAccumulators.TaxKey("US-FIT"), PayDay = 20261106, PeriodStartDay = 20261018, PeriodEndDay = 20261031, AmountMinor = 15615, TaxableWagesMinor = 200000 },
        };

        var totals = PayAccumulatorService.Sum(rows, new DateOnly(2026, 11, 27), 20261101, 20261114, "USD").For(PayAccumulators.TaxKey("US-FIT"));

        Assert.Equal((2000m, 4000m), (totals.PeriodTaxableWages, totals.YearTaxableWages));
    }

    private static (decimal Amount, decimal TaxableWages) Taxed(IReadOnlyList<PayrollTaxLine> lines, string taxCode)
    {
        var line = Assert.Single(lines, candidate => candidate.TaxCode == taxCode);

        return (Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero), line.TaxableWages);
    }

    private static PayrollTaxRequest Request(decimal wages, int periods = 26)
        => new()
        {
            Employer = new Employer { Registrations = [new EmployerTaxRegistration { Jurisdiction = "US" }] },
            PayDate = new DateOnly(2026, 11, 20),
            PeriodsPerYear = periods,
            Wages = new Dictionary<string, decimal>
            {
                [PayrollTaxCategories.IncomeTax] = wages,
                [PayrollTaxCategories.SocialInsurance] = wages,
                [PayrollTaxCategories.Unemployment] = wages,
            },
        };

    private static async Task<IReadOnlyList<PayrollTaxLine>> CalculateAsync(CalendarTestHarness harness, PayrollTaxRequest request)
        => await harness.RunAsync(async services =>
        {
            var result = await services.GetServices<IPayrollTaxProvider>().Single().CalculateAsync(request);

            Assert.Empty(result.Warnings);

            return (IReadOnlyList<PayrollTaxLine>)[.. result.Lines];
        }, commit: false);

    // Imports the shipped recipe: its tax tables into the catalog, its rules through the rules step (and their handler).
    private static async Task<IReadOnlyList<string>> ImportRecipeAsync(CalendarTestHarness harness)
    {
        var recipe = JsonNode.Parse(await File.ReadAllTextAsync(RecipePath(), TestContext.Current.CancellationToken))!.AsObject();
        var steps = recipe["steps"]!.AsArray();

        Assert.Equal(["CrestApps.OrchardCore.Payroll.TaxTables", PayrollConstants.Features.UnitedStates], steps.Single(step => step!["name"]!.GetValue<string>() == "Feature")!["enable"]!.AsArray().Select(feature => feature!.GetValue<string>()));

        var tables = steps.Single(step => step!["name"]!.GetValue<string>() == "TaxTable")!["TaxTables"].Deserialize<List<TaxTable>>(_options);
        var rules = steps.Single(step => step!["name"]!.GetValue<string>() == PayrollConstants.TaxRulesRecipeStepName)!.AsObject();

        return await harness.RunAsync(async services =>
        {
            var catalog = services.GetRequiredService<INamedCatalog<TaxTable>>();

            foreach (var table in tables)
            {
                await catalog.CreateAsync(table);
            }

            var context = new RecipeExecutionContext { Name = PayrollConstants.TaxRulesRecipeStepName, Step = rules.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<PayrollTaxRulesRecipeStep>(services).ExecuteAsync(context);

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

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The repository root was not found."), "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", "payroll-united-states-2026.recipe.json");
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddTaxTablesCore();
                services.AddPayrollCore();
                services.AddPayrollTaxTablesCore();
                services.AddScoped<PayrollTaxRuleTableUsage>();
                services.AddSingleton<INamedCatalog<TaxTable>>(new InMemoryNamedCatalog<TaxTable>());
                services.AddElectionFieldSet(UnitedStatesElectionFields.Create());
                services.AddPayrollWithholdingMethod<UnitedStatesFormW4WithholdingMethod>();
            },
            async schemaBuilder =>
            {
                await PayrollIndexSchema.CreateAsync(schemaBuilder);
                await PayrollTaxRuleIndexSchema.CreateAsync(schemaBuilder);
            },
            [.. PayrollIndexSchema.Providers(), new PayrollTaxRuleIndexProvider()]);
}
