using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core;
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
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
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
            request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = values }];
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

        request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = new() { ["FormYear"] = "2020", ["Exempt"] = "true" } }];

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
        request.TaxesToDate["US-SS-EE"] = new IncomeTaxToDate { YearTaxableIncome = 180000m };
        request.TaxesToDate["US-SS-ER"] = new IncomeTaxToDate { YearTaxableIncome = 180000m };
        request.TaxesToDate["US-FUTA"] = new IncomeTaxToDate { YearTaxableIncome = 7000m };
        request.YearIncome[IncomeTaxCategories.SocialInsurance] = 195000m;

        var lines = await CalculateAsync(harness, request);

        // 184500 - 180000 = 4500 at 6.2% = 279.00, each; Medicare on all 10000; Additional Medicare on 205000 - 200000.
        Assert.Equal((279.00m, 4500m), Taxed(lines, "US-SS-EE"));
        Assert.Equal((279.00m, 4500m), Taxed(lines, "US-SS-ER"));
        Assert.Equal((145.00m, 10000m), Taxed(lines, "US-MED-EE"));
        Assert.Equal((45.00m, 5000m), Taxed(lines, "US-MED-ADD"));
        Assert.DoesNotContain(lines, line => line.TaxCode == "US-FUTA");

        // Below the threshold no Additional Medicare line at all.
        request.YearIncome[IncomeTaxCategories.SocialInsurance] = 20000m;

        Assert.DoesNotContain(await CalculateAsync(harness, request), line => line.TaxCode == "US-MED-ADD");
    }

    [Fact]
    public async Task SupplementalPay_IsWithheldAt22Percent_And37PercentOverAMillionInTheYear_EvenWhenExempt()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(0m);

        request.SupplementalMethod = SupplementalWithholdingMethod.Flat;
        request.SupplementalIncome[IncomeTaxCategories.IncomeTax] = 5000m;
        request.YearSupplementalIncome[IncomeTaxCategories.IncomeTax] = 998000m;

        // 2000 under the million at 22% = 440; 3000 over it at 37% = 1110.
        Assert.Equal((1550.00m, 5000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));

        request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = new() { ["Exempt"] = "true" } }];

        Assert.Equal((1110.00m, 3000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));
    }

    [Fact]
    public async Task ABonusPaidOnItsOwn_ByTheAggregateMethod_IsWithheldOnThePeriodsTotal_WithoutExtraWithholdingAgain()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(1000m);

        // The regular pay of the period was 2000, withheld on already.
        request.TaxesToDate["US-FIT"] = new IncomeTaxToDate { PeriodTaxableIncome = 2000m };
        request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = new() { ["FormYear"] = "2020", ["FilingStatus"] = "Single", ["ExtraWithholding"] = "10" } }];

        // W(3000) = (78000 - 8600 = 69400: 5800 + 22% of 11500 = 8330) / 26 = 320.3846; W(2000) = 156.1538: 164.23.
        Assert.Equal((164.23m, 1000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));
    }

    [Fact]
    public async Task ABonusPaidAloneInAPeriodWithNoRegularPay_ByTheAggregateMethod_IsWithheldWithAPeriodOfRegularPay()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(0m);

        request.SupplementalIncome[IncomeTaxCategories.IncomeTax] = 1000m;
        request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = new() { ["FormYear"] = "2020", ["FilingStatus"] = "Single", ["ExtraWithholding"] = "10" } }];

        // Alone, the tables see 1000 as a whole period: W(1000) = (26000 - 8600 = 17400: 10% of 9900 = 990) / 26 = 38.08,
        // with the 10 of extra withholding: 48.08.
        Assert.Equal((48.08m, 1000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));

        // With the latest period's regular pay of 2000 it is added to (Pub. 15, supplemental wages paid separately):
        // W(3000) - W(2000) = 164.23, and no extra withholding again.
        request.RegularIncomePerPeriod[IncomeTaxCategories.IncomeTax] = 2000m;

        Assert.Equal((164.23m, 1000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));
    }

    [Fact]
    public void TaxRequest_ForABonusPaidAlone_CarriesAPeriodOfRegularPay_FromTheLatestStatementOrTheSalary()
    {
        var regular = new PayCode { ItemId = "reg", Code = "REG", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Regular };
        var bonus = new PayCode { ItemId = "bonus", Code = "BONUS", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Bonus };
        var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(regular);

        settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Taxed;
        regular.Put(settings);

        var bonusSettings = PayrollPayCodeSettingsExtensions.DefaultsFor(bonus);

        bonusSettings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Taxed;
        bonusSettings.Supplemental = true;
        bonus.Put(bonusSettings);

        var context = new PayCalculationContext
        {
            Codes = new Dictionary<string, PayCode> { ["reg"] = regular, ["bonus"] = bonus },
            PayDate = new DateOnly(2026, 11, 20),
            Schedule = new PaySchedule { Frequency = PayFrequency.Biweekly },
            Pay = new global::CrestApps.OrchardCore.Staff.Models.StaffPayRates { AnnualSalary = 52000m },
            History =
            [
                new PayStatement { PayDate = new DateOnly(2026, 10, 23), Lines = [new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = 1800m }] },
                new PayStatement { PayDate = new DateOnly(2026, 11, 6), Lines = [new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = 2100m }] },

                // A bonus paid alone earlier is no regular pay.
                new PayStatement { PayDate = new DateOnly(2026, 11, 13), Lines = [new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "bonus", Amount = 500m }] },
            ],
        };

        context.Lines.Add(new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "bonus", Amount = 5000m });

        var categories = new IncomeTaxCategoryOptions();

        categories.Categories.Add(new IncomeTaxCategory(IncomeTaxCategories.IncomeTax, global::OrchardCore.Localization.LocalizationSource.Create<UnitedStatesPayrollTests>("Income tax")));

        var step = new TaxesPayCalculationStep([], Options.Create(categories), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());

        // The latest statement with regular pay: 2100.
        Assert.Equal(2100m, step.Request(context).RegularIncomePerPeriod[IncomeTaxCategories.IncomeTax]);

        // No earlier statement: the salary over the periods, 52000 / 26.
        context.History = [];

        Assert.Equal(2000m, step.Request(context).RegularIncomePerPeriod[IncomeTaxCategories.IncomeTax]);

        // With regular pay of its own, the payment carries none.
        context.Lines.Add(new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = 2000m });

        Assert.Empty(step.Request(context).RegularIncomePerPeriod);
    }

    [Fact]
    public async Task RegularPayAfterAFlatRateBonusInThePeriod_IsWithheldByTheTablesAlone_WithItsExtraWithholding()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var request = Request(2000m);

        // A 5000 bonus paid on its own earlier in the period, withheld on at the flat 22% (1100).
        request.TaxesToDate["US-FIT"] = new IncomeTaxToDate { PeriodTaxableIncome = 5000m, PeriodFlatIncome = 5000m };
        request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = new() { ["FormYear"] = "2020", ["FilingStatus"] = "Single" } }];

        // W(2000) = 156.15, as for a period with no bonus; counting the bonus as wages already withheld on by the tables
        // gave W(7000) - W(5000) = 480.00.
        Assert.Equal((156.15m, 2000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));

        // Step 4(c) applies to that pay: 10 more.
        request.Elections = [new IncomeTaxElection { Jurisdiction = "US", Values = new() { ["FormYear"] = "2020", ["FilingStatus"] = "Single", ["ExtraWithholding"] = "10" } }];

        Assert.Equal((166.15m, 2000m), Taxed(await CalculateAsync(harness, request), "US-FIT"));

        // The bonus itself records its flat wages, and no extra withholding.
        var bonus = Request(0m);

        bonus.SupplementalMethod = SupplementalWithholdingMethod.Flat;
        bonus.SupplementalIncome[IncomeTaxCategories.IncomeTax] = 5000m;
        bonus.Elections = request.Elections;

        var line = Assert.Single(await CalculateAsync(harness, bonus), candidate => candidate.TaxCode == "US-FIT");

        Assert.Equal((1100.00m, 5000m), (Math.Round(line.Amount, 2), line.FlatIncome));
    }

    [Fact]
    public void TaxRequest_LowersSupplementalWagesByWhatPreTaxDeductionsLeft_AndSeesATaxableReimbursementAndContribution()
    {
        var regular = new PayCode { ItemId = "reg", Code = "REG", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Regular };
        var bonus = new PayCode { ItemId = "bonus", Code = "BONUS", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Bonus };
        var retirement = new PayCode { ItemId = "401k", Code = "401K", Class = PayCodeClass.Deduction, Kind = "Retirement" };
        var allowance = new PayCode { ItemId = "car", Code = "CAR", Class = PayCodeClass.Reimbursement, Kind = "Reimbursement" };

        static void Treat(PayCode code, PayCodeTaxTreatment treatment, bool supplemental = false)
        {
            var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(code);

            settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = treatment;
            settings.Supplemental = supplemental;
            code.Put(settings);
        }

        Treat(regular, PayCodeTaxTreatment.Taxed);
        Treat(bonus, PayCodeTaxTreatment.Taxed, supplemental: true);
        Treat(retirement, PayCodeTaxTreatment.Exempt);
        Treat(allowance, PayCodeTaxTreatment.Taxed);

        var context = new PayCalculationContext
        {
            Codes = new Dictionary<string, PayCode> { ["reg"] = regular, ["bonus"] = bonus, ["401k"] = retirement, ["car"] = allowance },
            PayDate = new DateOnly(2026, 11, 20),
        };

        context.Lines.Add(new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = 1000 });
        context.Lines.Add(new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "bonus", Amount = 2000 });
        context.Lines.Add(new PayStatementLine { Type = PayLineType.Deduction, PayCodeId = "401k", Amount = 1500, PayItemId = "item" });

        // The employer's match carries the deduction's code: it never lowers wages again.
        context.Lines.Add(new PayStatementLine { Type = PayLineType.EmployerContribution, PayCodeId = "401k", Amount = 500, PayItemId = "item", Payer = PayLinePayer.Employer });

        var categories = new IncomeTaxCategoryOptions();

        categories.Categories.Add(new IncomeTaxCategory(IncomeTaxCategories.IncomeTax, global::OrchardCore.Localization.LocalizationSource.Create<UnitedStatesPayrollTests>("Income tax")));

        var step = new TaxesPayCalculationStep([], Options.Create(categories), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());

        // 1000 regular less 1500 taken before tax: 0 regular, and the 500 left lowers the 2000 bonus to 1500.
        var request = step.Request(context);

        Assert.Equal((0m, 1500m), (request.Income[IncomeTaxCategories.IncomeTax], request.SupplementalIncome[IncomeTaxCategories.IncomeTax]));

        // Contributions and reimbursements now come before the taxes: a taxable car allowance adds to the wages.
        Assert.True(PayCalculationOrder.Contributions < PayCalculationOrder.Taxes && PayCalculationOrder.Contributions > PayCalculationOrder.PreTaxDeductions);

        context.Lines.Add(new PayStatementLine { Type = PayLineType.Reimbursement, PayCodeId = "car", Amount = 300, Payer = PayLinePayer.Employer });

        // 1000 + 300 - 1500 = -200: still no regular wages, and the bonus is lowered by 200 to 1800.
        var withAllowance = step.Request(context);

        Assert.Equal((0m, 1800m), (withAllowance.Income[IncomeTaxCategories.IncomeTax], withAllowance.SupplementalIncome[IncomeTaxCategories.IncomeTax]));
    }

    [Fact]
    public async Task AFormW4Rule_NeedsEveryTable_AndATableItUsesIsKept()
    {
        await using var harness = await CreateHarnessAsync();

        Assert.Empty(await ImportRecipeAsync(harness));

        var (missing, usages) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IIncomeTaxRuleManager>();
            var rule = (await manager.ListAsync()).Single(candidate => candidate.TaxCode == "US-FIT");

            var singleTableId = rule.Tables[UnitedStatesIncomeTax.FilingStatuses.Single];

            rule.ItemId = CrestApps.Core.UniqueId.GenerateId();
            rule.EffectiveFrom = new DateOnly(2027, 1, 1);
            rule.EffectiveTo = null;
            rule.Tables.Remove(UnitedStatesIncomeTax.FilingStatuses.HeadOfHousehold);

            var result = await manager.ValidateAsync(rule);
            var table = await services.GetRequiredService<INamedCatalog<TaxTable>>().FindByIdAsync(singleTableId);

            return (result, await services.GetRequiredService<IncomeTaxRuleTableUsage>().GetUsagesAsync(table));
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

        Assert.Equal(14, ids.Count);
        Assert.All(ids, id => Assert.True(CrestApps.Core.UniqueId.IsValid(id), id));
    }

    [Fact]
    public void FormW4Fields_AreTheUSElection_AndOtherJurisdictionsKeepTheGenericOne()
    {
        var options = new IncomeTaxElectionOptions();

        options.FieldSets.Add(GenericElectionFields.Create());
        options.FieldSets.Add(UnitedStatesElectionFields.Create());

        var federal = options.For("US");

        Assert.Equal(UnitedStatesIncomeTax.Jurisdiction, federal.Jurisdiction);
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

        var categories = new IncomeTaxCategoryOptions();

        categories.Categories.Add(new IncomeTaxCategory(IncomeTaxCategories.IncomeTax, global::OrchardCore.Localization.LocalizationSource.Create<UnitedStatesPayrollTests>("Income tax")));

        var step = new TaxesPayCalculationStep([], Options.Create(categories), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());
        var request = step.Request(context);

        Assert.Equal(202000m, request.YearIncome[IncomeTaxCategories.IncomeTax]);
        Assert.Equal(12000m, request.YearSupplementalIncome[IncomeTaxCategories.IncomeTax]);
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

        // A bonus withheld on at the flat rate in the same period is kept apart.
        var withBonus = rows.Append(new PayStatementLineIndex { LineKey = PayAccumulators.TaxKey("US-FIT"), PayDay = 20261118, PeriodStartDay = 20261101, PeriodEndDay = 20261114, AmountMinor = 110000, TaxableWagesMinor = 500000, FlatWagesMinor = 500000 });
        var bonusTotals = PayAccumulatorService.Sum(withBonus, new DateOnly(2026, 11, 27), 20261101, 20261114, "USD").For(PayAccumulators.TaxKey("US-FIT"));

        Assert.Equal((7000m, 5000m), (bonusTotals.PeriodTaxableWages, bonusTotals.PeriodFlatWages));
    }

    private static (decimal Amount, decimal TaxableIncome) Taxed(IReadOnlyList<IncomeTaxLine> lines, string taxCode)
    {
        var line = Assert.Single(lines, candidate => candidate.TaxCode == taxCode);

        return (Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero), line.TaxableIncome);
    }

    private static IncomeTaxRequest Request(decimal wages, int periods = 26)
        => new()
        {
            RegisteredJurisdictions = ["US"],
            PayDate = new DateOnly(2026, 11, 20),
            PeriodsPerYear = periods,
            Income = new Dictionary<string, decimal>
            {
                [IncomeTaxCategories.IncomeTax] = wages,
                [IncomeTaxCategories.SocialInsurance] = wages,
                [IncomeTaxCategories.Unemployment] = wages,
            },
        };

    private static async Task<IReadOnlyList<IncomeTaxLine>> CalculateAsync(CalendarTestHarness harness, IncomeTaxRequest request)
        => await harness.RunAsync(async services =>
        {
            var result = await services.GetServices<IIncomeTaxProvider>().Single().CalculateAsync(request);

            Assert.Empty(result.Warnings);

            return (IReadOnlyList<IncomeTaxLine>)[.. result.Lines];
        }, commit: false);

    // Imports the shipped recipe: its tax tables into the catalog, its rules through the rules step (and their handler).
    private static async Task<IReadOnlyList<string>> ImportRecipeAsync(CalendarTestHarness harness)
    {
        var recipe = JsonNode.Parse(await File.ReadAllTextAsync(RecipePath(), TestContext.Current.CancellationToken))!.AsObject();
        var steps = recipe["steps"]!.AsArray();

        Assert.Equal([TaxationConstants.Feature.IncomeTax, TaxationConstants.Feature.IncomeTaxUnitedStates], steps.Single(step => step!["name"]!.GetValue<string>() == "Feature")!["enable"]!.AsArray().Select(feature => feature!.GetValue<string>()));

        var tables = steps.Single(step => step!["name"]!.GetValue<string>() == "TaxTable")!["TaxTables"].Deserialize<List<TaxTable>>(_options);
        var rules = steps.Single(step => step!["name"]!.GetValue<string>() == TaxationConstants.IncomeTaxRulesRecipeStepName)!.AsObject();

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

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The repository root was not found."), "src", "Modules", "CrestApps.OrchardCore.Taxation", "Recipes", "income-tax-united-states-2026.recipe.json");
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddTaxTablesCore();
                services.AddPayrollCore();
                services.AddIncomeTaxCore();
                services.AddPayrollHandlers();
                services.AddScoped<IncomeTaxRuleTableUsage>();
                services.AddSingleton<INamedCatalog<TaxTable>>(new InMemoryNamedCatalog<TaxTable>());
                services.AddElectionFieldSet(UnitedStatesElectionFields.Create());
                services.AddIncomeTaxWithholdingMethod<UnitedStatesFormW4WithholdingMethod>();
            },
            async schemaBuilder =>
            {
                await PayrollIndexSchema.CreateAsync(schemaBuilder);
                await IncomeTaxRuleIndexSchema.CreateAsync(schemaBuilder);
            },
            [.. PayrollIndexSchema.Providers(), new IncomeTaxRuleIndexProvider()]);
}
