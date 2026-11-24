using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Pay code classes, tax treatment, limits and seeds: what a code adds to or takes from each tax's wages, the limits
/// that stop a line, the defaults of a code nobody set up, and the codes payroll adds.
/// </summary>
public sealed class PayCodeRulesTests
{
    [Fact]
    public void Deduction_WithAYearlyLimit_StopsAtTheLimit_AndStartsAgainWithTheNextYearsLimit()
    {
        var deduction = Code("RET", PayCodeClass.Deduction, PayCodeKinds.Contribution, settings => settings.YearLimits =
        [
            new DatedAmount { EffectiveFrom = new DateOnly(2026, 1, 1), Amount = 1000 },
            new DatedAmount { EffectiveFrom = new DateOnly(2027, 1, 1), Amount = 1200 },
        ]);
        var taken = new List<decimal>();
        var yearToDate = 0m;

        // Twelve pay periods of 100 in 2026: ten are taken in full, then the limit stops the line.
        for (var month = 1; month <= 12; month++)
        {
            var result = PayLimits.Apply(100, deduction, null, new PayLimitUsage { YearToDate = yearToDate }, new DateOnly(2026, month, 15));

            taken.Add(result.Amount);
            yearToDate += result.Amount;
        }

        var cut = PayLimits.Apply(100, deduction, null, new PayLimitUsage { YearToDate = 950 }, new DateOnly(2026, 11, 15));
        var newYear = PayLimits.Apply(100, deduction, null, new PayLimitUsage { YearToDate = 0 }, new DateOnly(2027, 1, 15));
        var lateNewYear = PayLimits.Apply(100, deduction, null, new PayLimitUsage { YearToDate = 1150 }, new DateOnly(2027, 12, 15));

        Assert.Equal([100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 0, 0], taken);
        Assert.Equal(1000, yearToDate);
        Assert.Equal(new PayLimitResult(50, PayLimitKind.Year, 50), cut);
        Assert.Equal(new PayLimitResult(100, PayLimitKind.None, 0), newYear);
        Assert.Equal(new PayLimitResult(50, PayLimitKind.Year, 50), lateNewYear);
    }

    [Fact]
    public void Limits_TheLowestRoomLeftWins_AcrossTheCodeAndTheItem()
    {
        var code = Code("GARN", PayCodeClass.Deduction, PayCodeKinds.Loan, settings =>
        {
            settings.PeriodLimit = 300;
            settings.LifetimeLimit = 5000;
        });
        var item = new EmployeePayItem { PeriodMaximum = 250, YearMaximum = 2000, Goal = 1000, PaidTowardGoal = 900 };
        var payDate = new DateOnly(2026, 6, 15);

        Assert.Equal(new PayLimitResult(100, PayLimitKind.Goal, 400), PayLimits.Apply(500, code, item, new PayLimitUsage(), payDate));
        Assert.Equal(PayLimitKind.Period, PayLimits.Apply(500, code, new EmployeePayItem(), new PayLimitUsage { ThisPeriod = 100 }, payDate).LimitedBy);
        Assert.Equal(200, PayLimits.Apply(500, code, new EmployeePayItem(), new PayLimitUsage { ThisPeriod = 100 }, payDate).Amount);
        Assert.Equal(150, PayLimits.Apply(500, code, new EmployeePayItem { PeriodMaximum = 250 }, new PayLimitUsage { ThisPeriod = 100 }, payDate).Amount);
        Assert.Equal(new PayLimitResult(0, PayLimitKind.Lifetime, 200), PayLimits.Apply(200, code, null, new PayLimitUsage { LifetimeToDate = 5200 }, payDate));
        Assert.Equal(new PayLimitResult(0, PayLimitKind.None, 0), PayLimits.Apply(-10, code, item, new PayLimitUsage(), payDate));
    }

    [Fact]
    public void TaxMatrix_ADeductionNotTaxedForIncomeTax_LowersOnlyIncomeTaxWages()
    {
        var retirement = Code("RET", PayCodeClass.Deduction, PayCodeKinds.Contribution, settings => settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Exempt);
        var health = Code("HLTH", PayCodeClass.Deduction, PayCodeKinds.Contribution, settings =>
        {
            settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Exempt;
            settings.TaxTreatment[IncomeTaxCategories.SocialInsurance] = PayCodeTaxTreatment.Exempt;
        });
        PayCodeAmount[] lines =
        [
            new(Code("REG", PayCodeClass.Earning, PayCodeKinds.Regular), 1000),
            new(Code("BONUS", PayCodeClass.Earning, PayCodeKinds.Bonus), 200),
            new(Code("CAR", PayCodeClass.ImputedIncome, "Benefit"), 50),
            new(Code("REIMB", PayCodeClass.Reimbursement, PayCodeKinds.Reimbursement), 100),
            new(Code("MATCH", PayCodeClass.EmployerContribution, PayCodeKinds.Contribution), 40),
            new(Code("LOAN", PayCodeClass.Deduction, PayCodeKinds.Loan), 75),
            new(retirement, 100),
            new(health, 30),
        ];

        Assert.Equal(1120, PayCodeTaxMatrix.WagesFor(IncomeTaxCategories.IncomeTax, lines));
        Assert.Equal(1220, PayCodeTaxMatrix.WagesFor(IncomeTaxCategories.SocialInsurance, lines));
        Assert.Equal(1250, PayCodeTaxMatrix.WagesFor(IncomeTaxCategories.Unemployment, lines));
        Assert.Equal(0, PayCodeTaxMatrix.WagesFor(IncomeTaxCategories.IncomeTax, [new(retirement, 100)]));
    }

    [Fact]
    public void Defaults_FollowTheClassAndKind_UntilSomeoneSetsTheCodeUp()
    {
        var regular = Code("REG", PayCodeClass.Earning, PayCodeKinds.Regular).GetPayrollSettings();
        var overtime = Code("OT", PayCodeClass.Earning, PayCodeKinds.Overtime).GetPayrollSettings();
        var holiday = Code("HOL", PayCodeClass.Earning, PayCodeKinds.Holiday).GetPayrollSettings();
        var bonus = Code("BONUS", PayCodeClass.Earning, PayCodeKinds.Bonus).GetPayrollSettings();
        var salary = Code("SAL", PayCodeClass.Earning, PayCodeKinds.Salary).GetPayrollSettings();
        var reimbursement = Code("REIMB", PayCodeClass.Reimbursement, PayCodeKinds.Reimbursement);
        var stored = Code("X", PayCodeClass.Earning, PayCodeKinds.Regular, settings => settings.Calculation = PayCodeCalculation.FixedAmount);

        Assert.Equal((PayCodeCalculation.HoursByRate, true, true, false), (regular.Calculation, regular.CountsInRegularRate, regular.CountsAsHoursWorked, regular.Supplemental));
        Assert.Equal((false, true), (overtime.CountsInRegularRate, overtime.CountsAsHoursWorked));
        Assert.Equal((PayCodeCalculation.HoursByRate, false, false), (holiday.Calculation, holiday.CountsInRegularRate, holiday.CountsAsHoursWorked));
        Assert.Equal((PayCodeCalculation.FixedAmount, true, true), (bonus.Calculation, bonus.Supplemental, bonus.CountsInRegularRate));
        Assert.Equal(PayCodeProrationRule.WorkingDays, salary.Proration);
        Assert.Equal(PayCodeTaxTreatment.Exempt, reimbursement.TreatmentFor(IncomeTaxCategories.IncomeTax));
        Assert.False(reimbursement.GetPayrollSettings().InRetroPay);
        Assert.Equal(PayCodeCalculation.FixedAmount, stored.GetPayrollSettings().Calculation);
        Assert.Equal(PayrollPayCodeSettingsExtensions.DefaultPriority, stored.GetPayrollSettings().Priority);
    }

    [Fact]
    public void Settings_WrittenByARecipe_WithNames_AreRead()
    {
        var code = Code("RET", PayCodeClass.Deduction, PayCodeKinds.Contribution);

        code.Properties[nameof(PayrollPayCodeSettings)] = JsonNode.Parse("""
            {
              "Calculation": "PercentOfEarnings",
              "TaxTreatment": { "IncomeTax": "Exempt" },
              "YearLimits": [ { "EffectiveFrom": "2026-01-01", "Amount": 23000 } ],
              "Arrears": "CarryForward",
              "Priority": 20
            }
            """);

        var settings = code.GetPayrollSettings();

        Assert.Equal(PayCodeCalculation.PercentOfEarnings, settings.Calculation);
        Assert.Equal(PayCodeTaxTreatment.Exempt, code.TreatmentFor(IncomeTaxCategories.IncomeTax));
        Assert.Equal(PayCodeTaxTreatment.Taxed, code.TreatmentFor(IncomeTaxCategories.SocialInsurance));
        Assert.Equal(23000, settings.YearLimitOn(new DateOnly(2026, 12, 31)));
        Assert.Null(settings.YearLimitOn(new DateOnly(2025, 12, 31)));
        Assert.Equal((PayCodeArrearsRule.CarryForward, 20), (settings.Arrears, settings.Priority));
    }

    [Fact]
    public async Task Seeder_AddsThePayrollCodesOnce_AndLeavesACodeAlreadyThere()
    {
        await using var harness = await CreateHarnessAsync();

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<PayCodeSeeder>().SeedAsync();

            var manager = services.GetRequiredService<IPayCodeManager>();
            var bonus = await manager.NewAsync();

            bonus.Code = "BONUS";
            bonus.Name = "Our own bonus";
            await manager.CreateAsync(bonus);
        });

        var first = await harness.RunAsync(services => services.GetRequiredService<PayrollPayCodeSeeder>().SeedAsync());
        var second = await harness.RunAsync(services => services.GetRequiredService<PayrollPayCodeSeeder>().SeedAsync());
        var codes = await harness.RunAsync(async services => (await services.GetRequiredService<IPayCodeManager>().GetAllAsync()).ToDictionary(code => code.Code), commit: false);

        Assert.Equal(["SAL", "COMM", "TIPS", "CTIPS", "TIPMU", "REIMB", "LOAN"], first);
        Assert.Empty(second);
        Assert.Equal("Our own bonus", codes["BONUS"].Name);
        Assert.False(codes["BONUS"].TryGet<PayrollPayCodeSettings>(out _));
        Assert.True(codes["LOAN"].TryGet<PayrollPayCodeSettings>(out var loan));
        Assert.Equal((PayCodeClass.Deduction, PayCodeArrearsRule.CarryForward, 200), (codes["LOAN"].Class, loan.Arrears, loan.Priority));
        Assert.Equal(PayCodeTaxTreatment.Exempt, codes["REIMB"].TreatmentFor(IncomeTaxCategories.IncomeTax));
        Assert.True(codes["SAL"].GetPayrollSettings().CountsAsHoursWorked);
    }

    [Fact]
    public async Task PayCode_ThatAnotherTakesAPercentOf_IsKept()
    {
        await using var harness = await CreateHarnessAsync();

        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayCodeManager>();
            var regular = await manager.FindByCodeAsync("REG");
            var pension = await manager.NewAsync();

            pension.Code = "PEN";
            pension.Name = "Pension";
            pension.Class = PayCodeClass.Deduction;
            pension.Put(new PayrollPayCodeSettings { Calculation = PayCodeCalculation.PercentOfEarnings, BaseCodeIds = [regular.ItemId] });
            await manager.CreateAsync(pension);
        });

        var (kept, other) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayCodeManager>();
            var failure = await Record.ExceptionAsync(async () => await manager.DeleteAsync(await manager.FindByCodeAsync("REG", TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
            var unused = await services.GetRequiredService<PayCodeBaseUsage>().GetUsagesAsync(await manager.FindByCodeAsync("OT"));

            return (failure, unused);
        }, commit: false);

        Assert.Contains("PEN", Assert.IsType<InvalidOperationException>(kept).Message, StringComparison.Ordinal);
        Assert.Empty(other);
    }

    [Fact]
    public void Card_ReadsNumbersWithADot_AndRefusesWhatTheClassDoesNotAllow()
    {
        var driver = Driver();
        var earning = Code("BONUS", PayCodeClass.Earning, PayCodeKinds.Bonus);
        var good = new PayrollPayCodeViewModel
        {
            Calculation = "PercentOfEarnings",
            BaseCodeIds = ["reg", earning.ItemId, "reg"],
            Treatments = new() { [IncomeTaxCategories.IncomeTax] = "Exempt", ["Unknown"] = "Exempt", [IncomeTaxCategories.Unemployment] = string.Empty },
            PeriodLimit = "250.50",
            YearLimits = [new() { EffectiveFrom = new DateOnly(2027, 1, 1), Amount = "1200" }, new() { EffectiveFrom = new DateOnly(2026, 1, 1), Amount = "1000" }, new()],
            Priority = 50,
            Arrears = "None",
            Proration = "CalendarDays",
            InRetroPay = true,
        };
        var (goodErrors, settings) = Read(driver, earning, good);

        var bad = new PayrollPayCodeViewModel
        {
            Calculation = "PercentOfDisposableEarnings",
            Arrears = "CarryForward",
            PeriodLimit = "12,5",
            YearLimits = [new() { EffectiveFrom = new DateOnly(2026, 1, 1) }, new() { EffectiveFrom = new DateOnly(2026, 3, 1), Amount = "5" }, new() { EffectiveFrom = new DateOnly(2026, 3, 1), Amount = "6" }],
            Priority = -1,
        };
        var (badErrors, _) = Read(driver, earning, bad);

        Assert.Equal(0, goodErrors);
        Assert.Equal(["reg"], settings.BaseCodeIds);
        Assert.Equal([IncomeTaxCategories.IncomeTax], settings.TaxTreatment.Keys);
        Assert.Equal(250.50m, settings.PeriodLimit);
        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)], settings.YearLimits.Select(limit => limit.EffectiveFrom));
        Assert.Equal((PayCodeProrationRule.CalendarDays, 50), (settings.Proration, settings.Priority));

        // Disposable earnings, arrears, the comma, the yearly limit without an amount, the same day twice and the priority.
        Assert.Equal(6, badErrors);
    }

    private static PayCode Code(string code, PayCodeClass payCodeClass, string kind, Action<PayrollPayCodeSettings> configure = null)
    {
        var item = new PayCode { ItemId = code.ToLowerInvariant(), Code = code, Name = code, Class = payCodeClass, Kind = kind };

        if (configure is not null)
        {
            var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(item);

            configure(settings);
            item.Put(settings);
        }

        return item;
    }

    private static PayrollPayCodeDisplayDriver Driver()
    {
        var categories = new IncomeTaxCategoryOptions();

        categories.Categories.Add(new IncomeTaxCategory(IncomeTaxCategories.IncomeTax, LocalizationSource.Create<PayCodeRulesTests>("Income tax")));
        categories.Categories.Add(new IncomeTaxCategory(IncomeTaxCategories.Unemployment, LocalizationSource.Create<PayCodeRulesTests>("Unemployment")));

        return new PayrollPayCodeDisplayDriver(
            new HttpContextAccessor(),
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IPayCodeManager>(),
            Options.Create(categories),
            Options.Create(new PayrollFormBoxOptions()),
            Mock.Of<CrestApps.OrchardCore.Taxation.Services.ITaxFormManager>(),
            Mock.Of<IStringLocalizerFactory>(),
            new PassThroughStringLocalizer<PayrollPayCodeDisplayDriver>());
    }

    private static (int Errors, PayrollPayCodeSettings Settings) Read(PayrollPayCodeDisplayDriver driver, PayCode code, PayrollPayCodeViewModel model)
    {
        var updater = new FakeUpdateModel(model);
        var context = new UpdateEditorContext(null, string.Empty, false, string.Empty, null, null, updater);
        var settings = driver.Read(code, model, context);

        return (updater.ModelState.ErrorCount, settings);
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddPayrollCore();
                services.AddPayrollHandlers();
                services.AddScoped<PayCodeBaseUsage>();
            },
            PayrollIndexSchema.CreateAsync,
            PayrollIndexSchema.Providers());
}
