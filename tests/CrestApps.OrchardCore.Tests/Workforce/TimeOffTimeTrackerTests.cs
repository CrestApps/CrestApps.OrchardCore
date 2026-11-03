using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Approved time off in Time Tracker: each day off is kept in the stage of its type once the day comes, and goes
/// away when the time off is canceled.
/// </summary>
public sealed class TimeOffTimeTrackerTests
{
    // Tuesday 3 November 2026.
    private static readonly DateOnly _tuesday = new(2026, 11, 3);

    [Fact]
    public async Task ApprovedTimeOff_IsKeptInItsTypesStage_OnceTheDayComes_AndCancelingTakesItAway()
    {
        // Arrange
        await using var kit = await Kit.CreateAsync();
        var setup = await kit.SetUpAsync();

        // Act (approved on Monday for Tuesday, then Tuesday comes)
        var approved = await TimeOffKit.SubmitAsync(kit.Harness, setup.Ana, setup.Vacation, _tuesday);
        var onMonday = await kit.EntriesAsync();

        kit.Now = new DateTime(2026, 11, 3, 20, 0, 0, DateTimeKind.Utc);
        await kit.Harness.RunAsync(services => services.GetRequiredService<TimeOffTimeTrackerRefresher>().RunAsync(_tuesday));
        var onTuesday = await kit.EntriesAsync();

        var canceled = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().CancelAsync(approved.Value.ItemId, "manager"));
        var afterCancel = await kit.EntriesAsync();
        var stored = await TimeOffKit.FindAsync(kit.Harness, approved.Value.ItemId);

        // Assert (a full day of 8 hours from 9:00 in the worker's zone)
        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors));
        Assert.Empty(onMonday);

        var entry = Assert.Single(onTuesday);

        Assert.Equal(setup.Vacation.StageId, entry.StageId);
        Assert.Equal(TimeClockKit.Local(_tuesday, 9), entry.StartUtc);
        Assert.Equal(TimeClockKit.Local(_tuesday, 17), entry.EndUtc);
        Assert.Equal(TimeOffTimeTracker.ClockId, entry.ManagedBy);
        Assert.Equal(TimeTrackerConstants.Sources.TimeOff, entry.Source);
        Assert.Equal(approved.Value.ItemId, entry.SourceRef);
        Assert.True(canceled.Succeeded, string.Join(" ", canceled.Errors));
        Assert.Empty(afterCancel);
        Assert.Equal(TimeOffRequestStatus.Canceled, stored.Status);
    }

    [Fact]
    public async Task TimeOffApprovedForADayThatHasCome_IsKeptAtOnce_AndAPartDayKeepsItsHours()
    {
        await using var kit = await Kit.CreateAsync();
        var setup = await kit.SetUpAsync();

        kit.Now = new DateTime(2026, 11, 3, 23, 0, 0, DateTimeKind.Utc);

        var approved = await TimeOffKit.SubmitAsync(kit.Harness, setup.Ana, setup.Vacation, _tuesday, configure: input =>
        {
            input.StartTime = new TimeOnly(13, 0);
            input.EndTime = new TimeOnly(17, 0);
        });
        var entry = Assert.Single(await kit.EntriesAsync());

        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors));
        Assert.Equal(TimeClockKit.Local(_tuesday, 13), entry.StartUtc);
        Assert.Equal(TimeClockKit.Local(_tuesday, 17), entry.EndUtc);
    }

    [Fact]
    public async Task EveryTimeOffType_GetsAStage_InTimeOff_AndPaidWhenItIsPaid()
    {
        await using var kit = await Kit.CreateAsync();

        var vacation = await TimeOffKit.AddTypeAsync(kit.Harness, "Vacation");
        var unpaid = await TimeOffKit.AddTypeAsync(kit.Harness, "Unpaid leave", type => type.IsPaid = false);
        var (stages, categories) = await kit.Harness.RunAsync(async services => (
            (await services.GetRequiredService<ITimeStageManager>().GetAllAsync()).ToDictionary(stage => stage.ItemId),
            (await services.GetRequiredService<ITimeCategoryManager>().GetAllAsync()).ToDictionary(category => category.ItemId, category => category.Name)), commit: false);

        Assert.Equal("Vacation", stages[vacation.StageId].Name);
        Assert.Equal(["Time off", "Paid"], stages[vacation.StageId].CategoryIds.Select(id => categories[id]));
        Assert.Equal(["Time off"], stages[unpaid.StageId].CategoryIds.Select(id => categories[id]));
    }

    private sealed class Kit : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; private set; }

        public DateTime Now { get; set; } = CalendarTestHarness.Now;

        public static async Task<Kit> CreateAsync()
        {
            var kit = new Kit();
            var clock = new Mock<IClock>();

            clock.SetupGet(c => c.UtcNow).Returns(() => kit.Now);

            kit.Harness = await TimeOffKit.CreateAsync(
                services =>
                {
                    services.AddTimeTrackerCore();
                    services.AddWorkforceTimeOffTimeTracker();
                    services.AddSingleton(clock.Object);
                },
                TimeTrackerIndexSchema.CreateAsync,
                [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
                [TimeTrackerConstants.CollectionName]);

            return kit;
        }

        public async Task<(WorkerProfile Ana, TimeOffType Vacation)> SetUpAsync()
        {
            var team = await WorkforceTests.AddTeamAsync(Harness, "Front desk", TeamKind.Team);
            var ana = await PlanningKit.AddMemberAsync(Harness, "Ana", team, worker => worker.UserId = "ana");
            var vacation = await TimeOffKit.AddTypeAsync(Harness, "Vacation");
            var policy = await TimeOffKit.AddPolicyAsync(Harness, vacation, team);

            await TimeOffKit.AddAllotmentAsync(Harness, team, slots: 2);
            await TimeOffKit.OpenBalanceAsync(Harness, ana, policy, 40);

            return (ana, vacation);
        }

        public Task<IReadOnlyList<TimeEntry>> EntriesAsync()
            => Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }
}
