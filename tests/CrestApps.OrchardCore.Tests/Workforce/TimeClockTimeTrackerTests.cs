using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tests.Workforce;

public sealed class TimeClockTimeTrackerTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task Punches_KeepTheWorkersDayInTimeTracker_InTheStagesOfTheirActivities()
    {
        // Arrange
        await using var kit = await CreateAsync();
        var team = await kit.AddTeamAsync("Field");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.UserId = "ana");
        var training = await ActivityAsync(kit, "TRN");

        // Act (9:00 in, 12:00 to 12:30 meal, 15:00 training, 17:00 out)
        await kit.PunchAtAsync(ana, TimePunchKind.In, _monday, 9);
        var whileIn = await EntriesAsync(kit);

        await kit.PunchAtAsync(ana, TimePunchKind.MealStart, _monday, 12);
        await kit.PunchAtAsync(ana, TimePunchKind.MealEnd, _monday, 12, 30);
        kit.Now = TimeClockKit.Local(_monday, 15);
        var change = await kit.PunchAsync(ana, TimePunchKind.ActivityChange, request => request.ActivityCodeId = training.ItemId);
        await kit.PunchAtAsync(ana, TimePunchKind.Out, _monday, 17);

        var entries = await EntriesAsync(kit);
        var stages = await StageNamesAsync(kit);
        var totals = Assert.Single(await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().GetTotalsAsync(new TimeEntryQuery { UserIds = ["ana"] }, "America/New_York", false), commit: false));
        var categories = await kit.Harness.RunAsync(async services => (await services.GetRequiredService<global::CrestApps.OrchardCore.TimeTracker.Core.Services.TimeCategoryManager>().GetAllAsync()).ToDictionary(category => category.Name, category => category.ItemId), commit: false);

        // Assert
        var open = Assert.Single(whileIn);
        Assert.Null(open.EndUtc);
        Assert.True(change.Succeeded, string.Join(" ", change.Errors));
        Assert.Equal(["Work", "Meal", "Work", "Training"], entries.Select(entry => stages[entry.StageId]));
        Assert.Equal(
            [(9, 0, 12, 0), (12, 0, 12, 30), (12, 30, 15, 0), (15, 0, 17, 0)],
            entries.Select(entry => (Local(entry.StartUtc).Hour, Local(entry.StartUtc).Minute, Local(entry.EndUtc.Value).Hour, Local(entry.EndUtc.Value).Minute)));
        Assert.All(entries, entry => Assert.Equal(TimeClockTimeTracker.ClockId, entry.ManagedBy));
        Assert.Equal(480, totals.TotalMinutes);
        Assert.Equal(450m, totals.MinutesByCategory[categories["Paid"]]);
        Assert.Equal(450m, totals.MinutesByCategory[categories["Worked"]]);
    }

    [Fact]
    public async Task ACorrectedPunch_MovesItsEntry_AndARemovedOneTakesItsTimeAway()
    {
        await using var kit = await CreateAsync();
        var team = await kit.AddTeamAsync("Field");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.UserId = "ana");

        await kit.PunchAtAsync(ana, TimePunchKind.In, _monday, 9);
        var mealStart = await kit.PunchAtAsync(ana, TimePunchKind.MealStart, _monday, 12);
        await kit.PunchAtAsync(ana, TimePunchKind.MealEnd, _monday, 12, 30);
        var punchOut = await kit.PunchAtAsync(ana, TimePunchKind.Out, _monday, 17);

        var corrected = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().CorrectAsync(punchOut.ItemId, TimePunchKind.Out, TimeClockKit.Local(_monday, 16), "Left at four.", "manager"));
        var removed = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().RemoveAsync(mealStart.ItemId, "Worked through lunch.", "manager"));
        var entries = await EntriesAsync(kit);

        Assert.True(corrected.Succeeded, string.Join(" ", corrected.Errors));
        Assert.True(removed.Succeeded, string.Join(" ", removed.Errors));
        Assert.Equal([(9, 16)], entries.Select(entry => (Local(entry.StartUtc).Hour, Local(entry.EndUtc.Value).Hour)));
    }

    [Fact]
    public async Task AWorkerWithoutALogin_IsLeftOut_AndALinkedWorkersTimeIsKeptByTheTimeClock_WhenTheirTeamUsesOne()
    {
        await using var kit = await CreateAsync();
        var team = await kit.AddTeamAsync("Field");
        var kiosk = await PlanningKit.AddMemberAsync(kit.Harness, "Kiosk only", team, worker => worker.UserId = null);
        await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.UserId = "ana");
        var office = await WorkforceTests.AddTeamAsync(kit.Harness, "Office", TeamKind.Team);
        await PlanningKit.AddMemberAsync(kit.Harness, "Bo", office, worker => worker.UserId = "bo");

        await kit.PunchAtAsync(kiosk, TimePunchKind.In, _monday, 9);

        var all = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery()), commit: false);
        var (anaClock, nobodyClock, boClock, switched) = await kit.Harness.RunAsync(async services =>
        {
            var clock = services.GetServices<IExternalTimeClock>().Single();
            var tracker = services.GetRequiredService<ITimeTrackerService>();
            var stage = (await services.GetRequiredService<global::CrestApps.OrchardCore.TimeTracker.Core.Services.TimeStageManager>().GetAllAsync()).First();

            return (await clock.FindAsync("ana"), await clock.FindAsync("someone"), await clock.FindAsync("bo"), await tracker.SwitchAsync("ana", "ana", stage.ItemId, TimeTrackerConstants.Sources.Self, "ana"));
        });

        Assert.Empty(all);
        Assert.Equal(TimeClockTimeTracker.ClockId, anaClock.Id);
        Assert.Null(nobodyClock);

        // A worker whose team has no time clock policy can't punch, so they keep their time in Time Tracker.
        Assert.Null(boClock);
        Assert.Equal("Your time is kept by the Time Clock.", Assert.Single(switched.Errors));
    }

    [Fact]
    public async Task TheBackfill_BringsEarlierPunchesIn_OnceForEachWorker()
    {
        await using var kit = await CreateAsync(sync: false);
        var team = await kit.AddTeamAsync("Field");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.UserId = "ana");

        await kit.WorkAsync(ana, _monday, 9, 17);
        await kit.WorkAsync(ana, _monday.AddDays(1), 8, 12);

        var before = await EntriesAsync(kit);
        var first = await kit.Harness.RunAsync(services => services.GetRequiredService<TimeClockTrackerBackfill>().RunAsync(10));
        var second = await kit.Harness.RunAsync(services => services.GetRequiredService<TimeClockTrackerBackfill>().RunAsync(10));
        var entries = await EntriesAsync(kit);

        Assert.Empty(before);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal([(9, 17), (8, 12)], entries.Select(entry => (Local(entry.StartUtc).Hour, Local(entry.EndUtc.Value).Hour)));
    }

    private static async Task<TimeClockKit> CreateAsync(bool sync = true)
    {
        var kit = await TimeClockKit.CreateAsync(
            services =>
            {
                services.AddTimeTrackerCore();
                services.AddWorkforceTimeClockTimeTracker();

                if (!sync)
                {
                    services.RemoveAll<ITimePunchObserver>();
                }
            },
            TimeTrackerIndexSchema.CreateAsync,
            [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
            [TimeTrackerConstants.CollectionName]);

        await kit.Harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());
        await kit.Harness.RunAsync(services => services.GetRequiredService<TimeClockStageSeeder>().SeedAsync());

        return kit;
    }

    private static Task<ActivityCode> ActivityAsync(TimeClockKit kit, string code)
        => kit.Harness.RunAsync(async services => (await services.GetRequiredService<IActivityCodeManager>().GetAllAsync()).Single(activity => activity.Code == code), commit: false);

    private static Task<IReadOnlyList<TimeEntry>> EntriesAsync(TimeClockKit kit)
        => kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

    private static Task<Dictionary<string, string>> StageNamesAsync(TimeClockKit kit)
        => kit.Harness.RunAsync(async services => (await services.GetRequiredService<global::CrestApps.OrchardCore.TimeTracker.Core.Services.TimeStageManager>().GetAllAsync()).ToDictionary(stage => stage.ItemId, stage => stage.Name), commit: false);

    private static DateTime Local(DateTime utc) => global::CrestApps.OrchardCore.Calendars.CalendarTime.ToLocal(utc, "America/New_York");
}
