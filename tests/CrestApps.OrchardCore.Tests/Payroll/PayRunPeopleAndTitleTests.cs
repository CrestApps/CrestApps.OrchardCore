using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Who a pay run pays and what it is called: a run of some people only, the people it may name, the company as the
/// employer of a run that chose none, and the name made for a run added without one.
/// </summary>
public sealed class PayRunPeopleAndTitleTests
{
    private static readonly PassThroughStringLocalizer<PayRunTitleHandler> _localizer = new();

    [Fact]
    public void Title_OfARegularRun_SaysThePeriod_AndTheScheduleOrThePeople()
    {
        var run = new PayRun { Type = PayRunType.Regular, PeriodStart = new DateOnly(2026, 11, 16), PeriodEnd = new DateOnly(2026, 11, 29), PayDate = new DateOnly(2026, 12, 4) };

        Assert.Equal("Pay for Nov 16 – Nov 29, 2026 (Weekly)", PayRunTitles.Build(run, "Weekly", [], _localizer, CultureInfo.InvariantCulture));
        Assert.Equal("Pay for Nov 16 – Nov 29, 2026: Jane Doe", PayRunTitles.Build(run, "Weekly", ["Jane Doe"], _localizer, CultureInfo.InvariantCulture));
        Assert.Equal("Pay for Nov 16 – Nov 29, 2026: 3 people", PayRunTitles.Build(run, "Weekly", ["A", "B", "C"], _localizer, CultureInfo.InvariantCulture));
        Assert.Equal("Pay for Nov 16 – Nov 29, 2026", PayRunTitles.Build(run, null, [], _localizer, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Title_OfARunAcrossAYear_AndOfARunOfInputsOnly_SaysTheYearsAndThePayDate()
    {
        var yearEnd = new PayRun { Type = PayRunType.Regular, PeriodStart = new DateOnly(2026, 12, 21), PeriodEnd = new DateOnly(2027, 1, 3), PayDate = new DateOnly(2027, 1, 8) };
        var final = new PayRun { Type = PayRunType.Final, PeriodStart = new DateOnly(2026, 11, 16), PeriodEnd = new DateOnly(2026, 11, 29), PayDate = new DateOnly(2026, 12, 5) };
        var bonus = new PayRun { Type = PayRunType.Supplemental, PeriodStart = final.PeriodStart, PeriodEnd = final.PeriodEnd, PayDate = final.PayDate };

        Assert.Equal("Pay for Dec 21, 2026 – Jan 3, 2027 (Weekly)", PayRunTitles.Build(yearEnd, "Weekly", [], _localizer, CultureInfo.InvariantCulture));
        Assert.Equal("Final pay for Jane Doe, paid Dec 5, 2026", PayRunTitles.Build(final, null, ["Jane Doe"], _localizer, CultureInfo.InvariantCulture));
        Assert.Equal("Bonus pay (Weekly), paid Dec 5, 2026", PayRunTitles.Build(bonus, "Weekly", [], _localizer, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Name_IsTheTitle_OrTheNumber_ForARunAddedBeforeTitles()
    {
        Assert.Equal("Bonus pay, paid Dec 5, 2026", PayRunTitles.Name(new PayRun { Number = "7", Title = "Bonus pay, paid Dec 5, 2026" }, _localizer));
        Assert.Equal("Pay run 700", PayRunTitles.Name(new PayRun { Number = "700" }, _localizer));
        Assert.Null(PayRunTitles.Trim("   "));
        Assert.Equal(PayRunTitles.MaxLength, PayRunTitles.Trim(new string('x', 300)).Length);
    }

    [Fact]
    public async Task ARunAddedWithoutAnEmployer_BelongsToTheCompany_AndIsNamedForItsSchedule()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);

        var (employerId, title) = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var existing = await runs.FindByIdAsync(setup.RunId);
            var run = await runs.NewAsync();

            run.PayScheduleId = existing.PayScheduleId;
            run.PeriodStart = new DateOnly(2026, 11, 15);
            run.PeriodEnd = new DateOnly(2026, 11, 28);
            run.PayDate = new DateOnly(2026, 12, 4);

            var validation = await runs.ValidateAsync(run);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
            await runs.CreateAsync(run);

            return (run.EmployerId, run.Title);
        });

        Assert.Equal(setup.EmployerId, employerId);
        Assert.StartsWith("Pay for Nov 15 – Nov 28, 2026", title);
        Assert.Contains("Every two weeks", title);
    }

    [Fact]
    public async Task ARunOfSomePeople_PaysOnlyThem_AndRefusesSomeoneWithNoPayProfile()
    {
        // Arrange: Lee has the setup's profile; Sam is on the same schedule; Kim is staff with no pay profile.
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);
        var sam = await WorkforceTests.AddWorkerAsync(harness, "Sam Ito", worker =>
        {
            worker.EmployeeNumber = "1002";
            worker.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 20 }] });
        });
        var kim = await WorkforceTests.AddWorkerAsync(harness, "Kim Park", worker => worker.EmployeeNumber = "1003");

        var errors = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var existing = await runs.FindByIdAsync(setup.RunId);
            var profiles = services.GetRequiredService<IPayProfileManager>();
            var profile = await profiles.NewAsync();

            profile.StaffMemberId = sam.ItemId;
            profile.PayScheduleId = existing.PayScheduleId;
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];
            await profiles.CreateAsync(profile);

            var run = await runs.NewAsync();

            run.PayScheduleId = existing.PayScheduleId;
            run.StaffMemberIds = [setup.StaffMemberId, kim.ItemId];
            run.PeriodStart = new DateOnly(2026, 11, 15);
            run.PeriodEnd = new DateOnly(2026, 11, 28);
            run.PayDate = new DateOnly(2026, 12, 4);

            return (await runs.ValidateAsync(run)).Errors.Select(error => error.ErrorMessage).ToList();
        });

        // Kim can't be paid: there is no pay profile of hers with the company.
        Assert.Contains(errors, error => error.Contains("Kim Park", StringComparison.Ordinal));

        var (paid, title) = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var existing = await runs.FindByIdAsync(setup.RunId);
            var run = await runs.NewAsync();

            run.PayScheduleId = existing.PayScheduleId;
            run.StaffMemberIds = [setup.StaffMemberId];
            run.PeriodStart = new DateOnly(2026, 11, 15);
            run.PeriodEnd = new DateOnly(2026, 11, 28);
            run.PayDate = new DateOnly(2026, 12, 4);
            await runs.CreateAsync(run);

            var result = await services.GetRequiredService<IPayRunService>().CalculateAsync(run, CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name));

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return ((await services.GetRequiredService<IPayRunService>().ListStatementsAsync(run)).Select(statement => statement.StaffMemberId).ToList(), run.Title);
        });

        // Only Lee is paid, not Sam, who is on the same schedule.
        Assert.Equal([setup.StaffMemberId], paid);
        Assert.EndsWith(": Lee Park", title);
    }
}
