using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Shared setup for the time clock tests: a harness with the time clock and a clock the test moves, weekly pay
/// periods from Sunday, the usual pay codes, and a policy on a team.
/// </summary>
internal sealed class TimeClockKit : IAsyncDisposable
{
    public static readonly DateOnly Monday = PlanningKit.Monday;

    private TimeClockKit(CalendarTestHarness harness)
    {
        Harness = harness;
    }

    public CalendarTestHarness Harness { get; }

    public DateTime Now { get; set; } = CalendarTestHarness.Now;

    public PayPeriodCalendar Calendar { get; private set; }

    public TimeClockPolicy Policy { get; private set; }

    public IReadOnlyDictionary<string, PayCode> Codes { get; private set; }

    public static async Task<TimeClockKit> CreateAsync(Action<IServiceCollection> configure = null)
    {
        TimeClockKit kit = null;
        var clock = new Mock<IClock>();

        clock.SetupGet(c => c.UtcNow).Returns(() => kit.Now);

        var harness = await WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeClockCore();
            services.AddSingleton<IDistributedLock, TimeOffKit.KeyedLock>();
            services.AddSingleton(clock.Object);
            configure?.Invoke(services);
        });

        kit = new TimeClockKit(harness);

        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        kit.Codes = (await harness.RunAsync(async services => await services.GetRequiredService<IPayCodeManager>().GetAllAsync(), commit: false))
            .ToDictionary(code => code.Code, StringComparer.Ordinal);
        kit.Calendar = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayPeriodCalendarManager>();
            var calendar = await manager.NewAsync();

            calendar.Name = "Weekly";
            calendar.Frequency = PayPeriodFrequency.Weekly;
            calendar.AnchorDate = Monday.AddDays(-1);
            calendar.WeekStart = DayOfWeek.Sunday;
            await manager.CreateAsync(calendar);

            return calendar;
        });

        return kit;
    }

    public async Task<WorkforceTeam> AddTeamAsync(string name, Action<TimeClockPolicy> configure = null)
    {
        var team = await WorkforceTests.AddTeamAsync(Harness, name, TeamKind.Team);

        Policy = await Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITimeClockPolicyManager>();
            var policy = await manager.NewAsync();

            policy.Name = name + " clock";
            policy.PayPeriodCalendarId = Calendar.ItemId;
            policy.EarlyGraceMinutes = 0;
            policy.LateGraceMinutes = 0;
            configure?.Invoke(policy);

            var validation = await manager.ValidateAsync(policy);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(policy);

            return policy;
        });

        return await PlanningKit.UpdateTeamAsync(Harness, team, stored => stored.TimeClockPolicyId = Policy.ItemId);
    }

    public Task<PlanningResult<TimePunch>> PunchAsync(WorkerProfile worker, TimePunchKind kind, Action<TimePunchRequest> configure = null)
    {
        return Harness.RunAsync(services =>
        {
            var request = new TimePunchRequest { WorkerId = worker.ItemId, Kind = kind, Source = TimePunchSource.Web };

            configure?.Invoke(request);

            return services.GetRequiredService<ITimeClockService>().PunchAsync(request, "tester");
        });
    }

    /// <summary>
    /// Records a punch at a local time, as a manager, and asserts it was recorded.
    /// </summary>
    public async Task<TimePunch> PunchAtAsync(WorkerProfile worker, TimePunchKind kind, DateOnly date, int hour, int minute = 0)
    {
        var utc = PlanningKit.Utc(date, new TimeOnly(hour, minute));

        // A punch is entered after it happens.
        if (Now < utc)
        {
            Now = utc.AddMinutes(1);
        }

        var result = await PunchAsync(worker, kind, request =>
        {
            request.Source = TimePunchSource.Manager;
            request.Utc = utc;
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Value;
    }

    /// <summary>
    /// Records a day of work from one local time to another.
    /// </summary>
    public async Task WorkAsync(WorkerProfile worker, DateOnly date, int fromHour, int toHour)
    {
        await PunchAtAsync(worker, TimePunchKind.In, date, fromHour);
        await PunchAtAsync(worker, TimePunchKind.Out, toHour >= 24 ? date.AddDays(1) : date, toHour % 24);
    }

    public async Task<Timesheet> TimesheetAsync(WorkerProfile worker, DateOnly? date = null)
    {
        var result = await Harness.RunAsync(services => services.GetRequiredService<ITimesheetService>().GetForDateAsync(worker, date ?? Monday));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Value;
    }

    public Task<IReadOnlyList<AttendanceException>> ExceptionsAsync(WorkerProfile worker)
    {
        return Harness.RunAsync(async services => (await services.GetRequiredService<IAttendanceService>().PageAsync(new AttendanceQuery { WorkerId = worker.ItemId }, 0, 100)).Items, commit: false);
    }

    public decimal Hours(Timesheet timesheet, string code, DateOnly? date = null)
    {
        return timesheet.Lines.Where(line => line.PayCodeId == Codes[code].ItemId && (date is null || line.Date == date)).Sum(line => line.Hours);
    }

    public static DateTime Local(DateOnly date, int hour, int minute = 0)
    {
        return PlanningKit.Utc(date, new TimeOnly(hour, minute));
    }

    public ValueTask DisposeAsync()
    {
        return Harness.DisposeAsync();
    }
}
