using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Retro pay, the tip credit, overtime re-priced by a bonus earned in earlier weeks, a year with an extra pay date and
/// a take-home amount grossed up on a run. Every amount is worked by hand in the comments.
/// </summary>
public sealed class PayAdjustmentsTests
{
    private static readonly PayCode _regular = Code("reg", "REG", PayCodeClass.Earning, PayCodeKinds.Regular);
    private static readonly PayCode _overtime = Code("ot", "OT", PayCodeClass.Earning, PayCodeKinds.Overtime, multiplier: 1.5m);
    private static readonly PayCode _salary = Code("sal", "SAL", PayCodeClass.Earning, PayCodeKinds.Salary);
    private static readonly PayCode _bonus = Code("bonus", "BONUS", PayCodeClass.Earning, PayCodeKinds.Bonus);
    private static readonly PayCode _tips = Code("ctips", "CTIPS", PayCodeClass.ImputedIncome, PayCodeKinds.Tips);
    private static readonly PayCode _makeup = Code("tipmu", "TIPMU", PayCodeClass.Earning, PayCodeKinds.MinimumWageMakeup);

    [Fact]
    public async Task RetroPay_PaysTheRaiseOnEarlierHours_LessWhatEarlierRetroAlreadyPaid()
    {
        // 80 hours paid at 20 from 18 to 31 October; the rate on file from 1 October is now 22: 80 x 2 = 160 owed.
        var earlier = Statement("s1", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 31), new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Hours = 80, Rate = 20, Amount = 1600, RatedHours = [new PayLineRatedHours { Date = new DateOnly(2026, 10, 25), Hours = 80 }] });
        var context = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 1), 22m)]);

        context.History = [earlier];
        await Step<RetroPayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(context.Lines);
        Assert.Equal((160m, "s1", "reg"), (line.Amount, line.RetroFor, line.PayCodeId));

        // A later statement already paid 100 of it: 60 is left.
        var retro = Statement("s2", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 14), new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = 100, RetroFor = "s1" });
        var again = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 1), 22m)], periodStart: new DateOnly(2026, 11, 15));

        again.History = [earlier, retro];
        await Step<RetroPayCalculationStep>().ExecuteAsync(again, TestContext.Current.CancellationToken);

        Assert.Equal(60m, Assert.Single(again.Lines).Amount);

        // A cut is never taken back, and a bonus run pays no retro.
        var cut = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 1), 18m)]);
        var bonusRun = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 1), 22m)]);

        cut.History = [earlier];
        bonusRun.History = [earlier];
        bonusRun.IncludeSalary = false;
        await Step<RetroPayCalculationStep>().ExecuteAsync(cut, TestContext.Current.CancellationToken);
        await Step<RetroPayCalculationStep>().ExecuteAsync(bonusRun, TestContext.Current.CancellationToken);

        Assert.Empty(cut.Lines);
        Assert.Empty(bonusRun.Lines);

        // A profile whose retro pay reaches back only to 1 November owes nothing for October.
        var limited = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 1), 22m)]);

        limited.History = [earlier];
        limited.Profile.RetroPayFrom = new DateOnly(2026, 11, 1);
        await Step<RetroPayCalculationStep>().ExecuteAsync(limited, TestContext.Current.CancellationToken);

        Assert.Empty(limited.Lines);
    }

    [Fact]
    public async Task RetroPay_PaysASalaryRaiseOnAFullEarlierPeriod()
    {
        // 52000 a year paid 2000 a period; 57200 from 1 October is 2200: 200 owed.
        var earlier = Statement("s1", new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 31), new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "sal", Amount = 2000 });
        var context = Context(salaries: [(new DateOnly(2020, 1, 1), 52000m), (new DateOnly(2026, 10, 1), 57200m)]);

        context.History = [earlier];
        await Step<RetroPayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(200m, Assert.Single(context.Lines).Amount);
    }

    [Fact]
    public async Task RetroPay_OwesNothingForARaiseInThePeriodAlreadyPaidByDay_LeavesAnInputsOwnRateAlone_AndPaysALaterRaiseByDay()
    {
        // 4 to 17 October, 20 an hour, 22 from 12 October: 40 hours before it were paid at 20 and 40 after at 22, the
        // training hours at their own 15. The rates on file haven't changed since: nothing is owed (the old comparison
        // with the period's last rate paid 40 x 2 = 80, and 10 x 7 = 70 on the training hours).
        var earlier = Statement("s1", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 17),
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Hours = 40, Rate = 20, Amount = 800, RatedHours = [new PayLineRatedHours { Date = new DateOnly(2026, 10, 6), Hours = 40 }] },
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Hours = 40, Rate = 22, Amount = 880, RatedHours = [new PayLineRatedHours { Date = new DateOnly(2026, 10, 13), Hours = 40 }] },
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Hours = 10, Rate = 15, Amount = 150 });
        var unchanged = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 12), 22m)]);

        unchanged.History = [earlier];
        await Step<RetroPayCalculationStep>().ExecuteAsync(unchanged, TestContext.Current.CancellationToken);

        Assert.Empty(unchanged.Lines);

        // A raise to 24 back to 1 October, entered later: 40 x (24 - 20) + 40 x (24 - 22) = 240; the training hours
        // still nothing.
        var raised = Context(rates: [(new DateOnly(2020, 1, 1), 20m), (new DateOnly(2026, 10, 1), 24m)]);

        raised.History = [earlier];
        await Step<RetroPayCalculationStep>().ExecuteAsync(raised, TestContext.Current.CancellationToken);

        Assert.Equal(240m, Assert.Single(raised.Lines).Amount);
    }

    [Fact]
    public async Task RetroPay_OwesNothingForASalaryChangeInThePeriodAlreadyProratedByDay()
    {
        // 52000 a year, 62400 from 11 October, in the period of 4 to 17 October: the period was paid day by day at the
        // salary of each day; with the same salaries on file, nothing is owed (the old comparison paid 62400 / 26 = 2400
        // less what was paid).
        var context = Context(salaries: [(new DateOnly(2020, 1, 1), 52000m), (new DateOnly(2026, 10, 11), 62400m)]);
        var paid = context.Round(EarningsPayCalculationStep.SalaryFor(context, _salary, new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 17)));

        Assert.InRange(paid, 2000.01m, 2399.99m);

        context.History = [Statement("s1", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 17), new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "sal", Amount = paid })];
        await Step<RetroPayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(context.Lines);
    }

    [Fact]
    public async Task ABonusEarnedOverPartOfAPeriod_CountsThatPeriodsShareOfHours_AndSaysSo()
    {
        // Earned 10 to 17 October: the period of 4 to 17 October is 8 of its 14 days in the window. Its 80 regular and
        // 10 overtime hours count 8/14 each: the 900 raises the rate by 900 / (90 x 8/14) = 17.50, owed half on each of
        // the 10 x 8/14 overtime hours = 50.00. Before, the period was left out and nothing was owed.
        var earlier = Statement("s1", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 17),
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Hours = 80, Rate = 20, Amount = 1600 },
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "ot", Hours = 10, Rate = 30, Amount = 300 });
        var context = Context(rates: [(new DateOnly(2020, 1, 1), 20m)]);

        context.History = [earlier];
        context.Inputs = [new PayInput { PayCodeId = "bonus", Amount = 900, EarnedFrom = new DateOnly(2026, 10, 10), EarnedTo = new DateOnly(2026, 10, 17), PeriodStart = context.PeriodStart, PeriodEnd = context.PeriodEnd }];
        await Step<EarnedRegularRatePayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(50m, Assert.Single(context.Lines).Amount);
        Assert.Contains(context.Warnings, warning => warning.Contains("partly in it", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(100, 104.80)]
    [InlineData(300, 0)]
    public async Task TipCredit_MakesAWeekUpToTheMinimumWage_CountingAtMostTheMaximumCreditAnHour(decimal tips, decimal expected)
    {
        // 40 hours at a 2.13 cash wage is 85.20; the week needs 40 x 7.25 = 290. Tips of 100 count fully (40 x 5.12 =
        // 204.80 at most): 290 - 85.20 - 100 = 104.80. Tips of 300 count 204.80: nothing short.
        var context = Context(rates: [(new DateOnly(2020, 1, 1), 2.13m)]);

        context.Profile.Tipped = true;
        context.MinimumHourlyWage = 7.25m;
        context.MaximumTipCredit = 5.12m;
        context.Inputs = [Input("reg", hours: 40, date: new DateOnly(2026, 11, 3)), Input("ctips", amount: tips, date: new DateOnly(2026, 11, 4))];
        await Step<TipCreditPayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(expected, context.Lines.Where(line => line.PayCodeId == "tipmu").Sum(line => line.Amount));
    }

    [Fact]
    public async Task ABonusEarnedInEarlierWeeks_ReprisesTheirOvertime()
    {
        // 4 to 17 October: 80 regular and 10 overtime hours. A 900 bonus earned then raises the regular rate by 900 / 90
        // = 10 an hour; each overtime hour is owed half of it: 10 x 0.5 x 10 = 50.
        var earlier = Statement("s1", new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 17),
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Hours = 80, Rate = 20, Amount = 1600 },
            new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "ot", Hours = 10, Rate = 30, Amount = 300 });
        var context = Context(rates: [(new DateOnly(2020, 1, 1), 20m)]);

        context.History = [earlier];
        context.Inputs = [new PayInput { PayCodeId = "bonus", Amount = 900, EarnedFrom = new DateOnly(2026, 10, 4), EarnedTo = new DateOnly(2026, 10, 17), PeriodStart = context.PeriodStart, PeriodEnd = context.PeriodEnd }];
        await Step<EarnedRegularRatePayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(context.Lines);
        Assert.Equal((50m, "ot"), (line.Amount, line.PayCodeId));

        // An exempt person is owed no overtime.
        context.Lines.Clear();
        context.Profile.OvertimeStatus = OvertimeStatus.Exempt;
        await Step<EarnedRegularRatePayCalculationStep>().ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(context.Lines);
    }

    [Fact]
    public async Task AYearWithAnExtraPayDate_DividesTheSalaryByItsPayDates_WhenTheScheduleSaysSo()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.Biweekly, AnchorDate = new DateOnly(2026, 1, 4), PayDateOffsetDays = 5 };
        var counts = Enumerable.Range(2026, 14).Select(year => PayCalculationContextFactory.PayDatesIn(schedule, year)).ToList();

        // Every two weeks, most years have 26 pay dates and some 27.
        Assert.All(counts, count => Assert.InRange(count, 26, 27));
        Assert.Contains(27, counts);

        // 52000 over 27 pay dates is 1925.93 a pay, over the usual 26 it is 2000.
        var context = Context(salaries: [(new DateOnly(2020, 1, 1), 52000m)]);

        context.SalaryPeriods = 27;
        context.Codes = new Dictionary<string, PayCode> { ["sal"] = _salary };
        await new EarningsPayCalculationStep(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<EarningsPayCalculationStep>()).ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(1925.93m, Assert.Single(context.Lines).Amount);
    }

    [Fact]
    public async Task ATakeHomeAmount_IsGrossedUp_SoNetPayRisesByExactlyThatMuch()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        var statement = await harness.RunAsync(async services =>
        {
            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var input = await inputs.NewAsync();

            input.RunId = setup.RunId;
            input.StaffMemberId = setup.StaffMemberId;
            input.PayCodeId = setup.BonusCodeId;
            input.Amount = 500;
            input.IsNet = true;
            await inputs.CreateAsync(input);

            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            Assert.True((await service.CalculateAsync(run, preparer)).Succeeded);

            return Assert.Single(await service.ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)));
        });

        // 80 hours at 20 nets 1500.80 after 6.2%; 500 more to take home needs a gross of 500 / 0.938 = 533.05.
        Assert.Equal(2000.80m, statement.Net);
        Assert.Equal(533.05m, statement.Lines.Single(line => line.PayCodeId == setup.BonusCodeId).Amount);
    }

    [Fact]
    public async Task ATakeHomeAmount_PaysOnlyTheTaxes_AGarnishmentIsTakenFromItAsFromAnyPay()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddGarnishmentsCore());
        var setup = await PayRunTests.SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        var statement = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<CrestApps.OrchardCore.Payroll.Core.Services.GarnishmentPayCodeSeeder>().SeedAsync();

            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();
            var orders = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await orders.NewAsync();

            order.ProfileId = profile.ItemId;
            order.Type = GarnishmentType.Support;
            order.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("GARN")).ItemId;
            order.Amount = 100;
            order.StartDate = new DateOnly(2026, 1, 1);
            order.PaymentMethod = GarnishmentPaymentMethod.Outside;
            await orders.CreateAsync(order);

            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var input = await inputs.NewAsync();

            input.RunId = setup.RunId;
            input.StaffMemberId = setup.StaffMemberId;
            input.PayCodeId = setup.BonusCodeId;
            input.Amount = 500;
            input.IsNet = true;
            await inputs.CreateAsync(input);

            var service = services.GetRequiredService<IPayRunService>();

            Assert.True((await service.CalculateAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), preparer)).Succeeded);

            return Assert.Single(await service.ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)));
        });

        // The bonus is still 533.05 (its taxes paid); the 100 of support comes out of the pay: 1500.80 + 500 - 100.
        Assert.Equal(533.05m, statement.Lines.Single(line => line.PayCodeId == setup.BonusCodeId).Amount);
        Assert.Equal(100m, statement.Lines.Single(line => line.GarnishmentOrderId is not null).Amount);
        Assert.Equal(1900.80m, statement.Net);
    }

    private static T Step<T>()
        where T : class
        => (T)Activator.CreateInstance(typeof(T), Activator.CreateInstance(typeof(CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<>).MakeGenericType(typeof(T))));

    private static PayCode Code(string id, string code, PayCodeClass payCodeClass, string kind, decimal multiplier = 1)
        => new() { ItemId = id, Code = code, Name = code, Class = payCodeClass, Kind = kind, Multiplier = multiplier };

    private static PayInput Input(string payCodeId, decimal? hours = null, decimal? amount = null, DateOnly? date = null)
        => new() { PayCodeId = payCodeId, Hours = hours, Amount = amount, Date = date, SourceKind = "Test", SourceId = Guid.NewGuid().ToString("n") };

    private static PayStatement Statement(string id, DateOnly start, DateOnly end, params PayStatementLine[] lines)
        => new() { ItemId = id, PeriodStart = start, PeriodEnd = end, PayDate = end.AddDays(5), Status = PayStatementStatus.Paid, Lines = [.. lines] };

    private static PayCalculationContext Context((DateOnly From, decimal Rate)[] rates = null, (DateOnly From, decimal Salary)[] salaries = null, DateOnly? periodStart = null)
    {
        var start = periodStart ?? new DateOnly(2026, 11, 1);
        var pay = new StaffPayRates { PayBasis = salaries is null ? StaffPayBasis.Hourly : StaffPayBasis.Salary };

        foreach (var (from, rate) in rates ?? [])
        {
            pay.Rates.Add(new StaffPayRate { EffectiveFrom = from, HourlyRate = rate });
        }

        foreach (var (from, salary) in salaries ?? [])
        {
            pay.Salaries.Add(new StaffSalary { EffectiveFrom = from, AnnualAmount = salary });
        }

        return new PayCalculationContext
        {
            Profile = new EmployeePayProfile { StaffMemberId = "staff", EmployerId = "employer" },
            Employer = new Employer { Currency = "USD" },
            Schedule = new PaySchedule { Frequency = PayFrequency.Biweekly, WeekStart = DayOfWeek.Sunday },
            PeriodStart = start,
            PeriodEnd = start.AddDays(13),
            PayDate = start.AddDays(19),
            Pay = pay,
            Codes = new Dictionary<string, PayCode>
            {
                ["reg"] = _regular,
                ["ot"] = _overtime,
                ["sal"] = _salary,
                ["bonus"] = _bonus,
                ["ctips"] = _tips,
                ["tipmu"] = _makeup,
            },
        };
    }
}
