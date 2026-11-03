using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.TimeTracker;

public sealed class TimeTrackerTests
{
    // Monday 2 November 2026, 9:00 in New York.
    private static readonly DateTime _nine = new(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SwitchingStage_EndsTheOneBefore_AndTheDayAddsUpByCategory()
    {
        // Arrange (Payable counts Work and Lunch is unpaid; Billable counts only client work; Overtime counts half again)
        await using var kit = await Kit.CreateAsync();
        var payable = await kit.CategoryAsync("Payable");
        var billable = await kit.CategoryAsync("Billable");
        var breaks = await kit.CategoryAsync("Break");
        var work = await kit.StageAsync("Work", payable, billable);
        var admin = await kit.StageAsync("Admin", payable);
        var lunch = await kit.StageAsync("Lunch", breaks);

        // Act (9:00 work, 11:00 admin, 12:00 lunch, 12:30 work, 17:00 out)
        await kit.SwitchAsync(work, 0);
        await kit.SwitchAsync(admin, 120);
        await kit.SwitchAsync(lunch, 180);
        await kit.SwitchAsync(work, 210);

        kit.Now = _nine.AddMinutes(480);

        var outResult = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ClockOutAsync("ana", "ana"));
        var again = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ClockOutAsync("ana", "ana"));
        var entries = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);
        var totals = Assert.Single(await kit.TotalsAsync(false));

        // Assert
        Assert.True(outResult.Succeeded);
        Assert.Equal("Not clocked in.", Assert.Single(again.Errors));
        Assert.Equal(4, entries.Count);
        Assert.All(entries, entry => Assert.NotNull(entry.EndUtc));
        Assert.Equal(entries.Select(entry => entry.EndUtc.Value).Take(3), entries.Skip(1).Select(entry => entry.StartUtc));
        Assert.Equal(480, totals.TotalMinutes);
        Assert.Equal(450m, totals.MinutesByCategory[payable.ItemId]);
        Assert.Equal(390m, totals.MinutesByCategory[billable.ItemId]);
        Assert.Equal(30m, totals.MinutesByCategory[breaks.ItemId]);
        Assert.Equal(390, totals.MinutesByStage[work.ItemId]);
    }

    [Fact]
    public async Task ACategoryFactor_CountsItsMinutesAgain_AndOpenTimeCountsUpToNow()
    {
        await using var kit = await Kit.CreateAsync();
        var premium = await kit.CategoryAsync("Overtime premium", 1.5m);
        var overtime = await kit.StageAsync("Overtime", premium);

        await kit.SwitchAsync(overtime, 0);

        kit.Now = _nine.AddMinutes(60);

        var totals = Assert.Single(await kit.TotalsAsync(false));

        Assert.Equal(60, totals.TotalMinutes);
        Assert.Equal(90m, totals.MinutesByCategory[premium.ItemId]);
    }

    [Fact]
    public async Task TotalsByDay_SplitAnEntryAtLocalMidnight()
    {
        await using var kit = await Kit.CreateAsync();
        var payable = await kit.CategoryAsync("Payable");
        var night = await kit.StageAsync("Night work", payable);

        // 22:00 to 02:00 in New York.
        var start = new DateTime(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc);

        kit.Now = start.AddHours(10);

        var added = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().AddAsync(
            new TimeEntry { UserId = "ana", UserName = "ana", StageId = night.ItemId, StartUtc = start, EndUtc = start.AddHours(4) }, "manager", "Forgot to clock in."));
        var days = await kit.TotalsAsync(true, start.AddDays(-1), start.AddDays(1));

        Assert.True(added.Succeeded, string.Join(" ", added.Errors));
        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)], days.Select(row => row.Date.Value));
        Assert.Equal([120, 120], days.Select(row => row.TotalMinutes));
    }

    [Fact]
    public async Task AManagerEdit_NeedsAReason_KeepsWhatItWas_TellsObservers_AndOverlapsAreRefused()
    {
        await using var kit = await Kit.CreateAsync();
        var payable = await kit.CategoryAsync("Payable");
        var work = await kit.StageAsync("Work", payable);
        var lunch = await kit.StageAsync("Lunch");

        await kit.SwitchAsync(work, 0);
        await kit.SwitchAsync(lunch, 180);
        await kit.SwitchAsync(work, 240);

        kit.Now = _nine.AddMinutes(300);

        var entries = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);
        var lunchEntry = entries[1];

        var noReason = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().UpdateAsync(lunchEntry.ItemId, lunch.ItemId, _nine.AddMinutes(180), _nine.AddMinutes(210), null, "manager", null));
        var overlap = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().UpdateAsync(lunchEntry.ItemId, lunch.ItemId, _nine.AddMinutes(170), _nine.AddMinutes(210), null, "manager", "Lunch was shorter."));
        var shorter = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().UpdateAsync(lunchEntry.ItemId, lunch.ItemId, _nine.AddMinutes(180), _nine.AddMinutes(210), "Back early", "manager", "Lunch was shorter."));
        var removed = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().DeleteAsync(lunchEntry.ItemId, "manager", null));

        Assert.Equal("Say why the time is changed.", Assert.Single(noReason.Errors));
        Assert.StartsWith("It overlaps other time from", Assert.Single(overlap.Errors));
        Assert.True(shorter.Succeeded, string.Join(" ", shorter.Errors));

        var change = Assert.Single(shorter.Entry.Changes);

        Assert.Equal("Lunch 2026-11-02 17:00-2026-11-02 18:00 UTC", change.Before);
        Assert.Equal("Lunch was shorter.", change.Reason);
        Assert.Equal("manager", change.By);
        Assert.Equal("Say why the time is removed.", Assert.Single(removed.Errors));
        Assert.Contains((TimeEntryChangeKind.Edited, "Lunch was shorter."), kit.Changes);
    }

    [Fact]
    public async Task TheStageAndCategoryNames_AreRequired_AndUnique()
    {
        await using var kit = await Kit.CreateAsync();

        await kit.CategoryAsync("Payable");

        var duplicate = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<TimeCategoryManager>();
            var category = await manager.NewAsync();

            category.Name = " payable ";

            return await manager.ValidateAsync(category);
        }, commit: false);

        Assert.False(duplicate.Succeeded);
        Assert.Equal("Another category is called payable.", Assert.Single(duplicate.Errors).ErrorMessage);
    }

    [Fact]
    public async Task TheRecipeStep_SetsUpCategoriesAndStages_ByName_AndRunningItAgainChangesNothing()
    {
        await using var kit = await Kit.CreateAsync();
        var step = System.Text.Json.Nodes.JsonNode.Parse("""
            {
              "name": "TimeTracker",
              "Categories": [ { "name": "Payable", "code": "PAY" }, { "name": "Billable", "position": 2 } ],
              "Stages": [
                { "name": "Work", "code": "WORK", "categories": [ "Payable", "Billable" ] },
                { "name": "Lunch", "categories": [ "Unknown" ] }
              ]
            }
            """).AsObject();

        var errors = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            await kit.Harness.RunAsync(async services =>
            {
                var users = new Mock<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>(Mock.Of<Microsoft.AspNetCore.Identity.IUserStore<global::OrchardCore.Users.IUser>>(), null, null, null, null, null, null, null, null);
                var recipe = ActivatorUtilities.CreateInstance<CrestApps.OrchardCore.TimeTracker.Recipes.TimeTrackerRecipeStep>(services, users.Object);
                var context = new global::OrchardCore.Recipes.Models.RecipeExecutionContext { Name = TimeTrackerConstants.RecipeStepName, Step = step.DeepClone().AsObject() };

                await recipe.ExecuteAsync(context);
                errors.AddRange(context.Errors);
            });
        }

        var (categories, stages) = await kit.Harness.RunAsync(async services => (await services.GetRequiredService<TimeCategoryManager>().GetAllAsync(), await services.GetRequiredService<TimeStageManager>().GetAllAsync()), commit: false);
        var work = Assert.Single(stages, stage => stage.Name == "Work");

        Assert.Equal(2, categories.Count());
        Assert.Equal(2, stages.Count());
        Assert.Equal(categories.Select(category => category.ItemId).Order(), work.CategoryIds.Order());
        Assert.Equal("WORK", work.Code);
        Assert.Equal(2, errors.Count(error => error.Contains("'Unknown'", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Sync_ReplacesOnlyItsOwnEntries_KeepsTheUnchangedOnes_AndWorksAroundOtherTime()
    {
        // Arrange (a manager recorded lunch from 12:00 to 12:30; the clock says work from 9:00 to 13:00)
        await using var kit = await Kit.CreateAsync();
        var work = await kit.StageAsync("Work");
        var lunch = await kit.StageAsync("Lunch");

        kit.Now = _nine.AddHours(6);

        var manual = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().AddAsync(
            new TimeEntry { UserId = "ana", UserName = "ana", StageId = lunch.ItemId, StartUtc = _nine.AddHours(3), EndUtc = _nine.AddHours(3.5) }, "manager", "Lunch"));

        // Act
        var first = await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine, EndUtc = _nine.AddHours(4), SourceRef = "p1" });
        var firstIds = await kit.ManagedIdsAsync();
        var again = await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine, EndUtc = _nine.AddHours(4), SourceRef = "p1" });
        var againIds = await kit.ManagedIdsAsync();
        var shorter = await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine, EndUtc = _nine.AddHours(2), SourceRef = "p1" });
        var entries = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

        // Assert
        Assert.True(manual.Succeeded, string.Join(" ", manual.Errors));
        Assert.True(first.Succeeded && again.Succeeded && shorter.Succeeded);
        Assert.Equal(2, firstIds.Count);
        Assert.Equal(firstIds, againIds);
        Assert.Equal([(_nine, _nine.AddHours(2)), (_nine.AddHours(3), _nine.AddHours(3.5))], entries.Select(entry => (entry.StartUtc, entry.EndUtc.Value)));
        Assert.Equal([Kit.ClockId, null], entries.Select(entry => entry.ManagedBy));
    }

    [Fact]
    public async Task TimeLeftOpenHere_EndsWhereTheOtherClocksTimeStarts()
    {
        // Ana switched to Work here at 9:00, and later the clock says she clocked in at 10:00.
        await using var kit = await Kit.CreateAsync();
        var work = await kit.StageAsync("Work");

        await kit.SwitchAsync(work, 0);

        kit.Now = _nine.AddHours(2);

        await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine.AddHours(1) });

        var entries = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

        Assert.Equal([(_nine, (DateTime?)_nine.AddHours(1), null), (_nine.AddHours(1), null, Kit.ClockId)], entries.Select(entry => (entry.StartUtc, entry.EndUtc, entry.ManagedBy)));
        Assert.Contains((TimeEntryChangeKind.Ended, null), kit.Changes);
    }

    [Fact]
    public async Task ARangeSync_KeepsTheTimeBeforeItsRange_AndGoesOnWithTheEntryOfTheSameStage()
    {
        // Ana was available from 9:00; at 10:30 she took a call. Each sync only knows the last few minutes.
        await using var kit = await Kit.CreateAsync();
        var available = await kit.StageAsync("Available");
        var onCall = await kit.StageAsync("On a call");

        kit.Now = _nine.AddMinutes(30);
        await kit.RangeSyncAsync(_nine.AddMinutes(-10), [], new TimeEntry { StageId = available.ItemId, StartUtc = _nine });
        var first = Assert.Single(await kit.EntriesAsync());

        kit.Now = _nine.AddMinutes(90);
        await kit.RangeSyncAsync(_nine.AddMinutes(80), [], new TimeEntry { StageId = available.ItemId, StartUtc = _nine.AddMinutes(80), EndUtc = _nine.AddMinutes(90) }, new TimeEntry { StageId = onCall.ItemId, StartUtc = _nine.AddMinutes(90) });
        var entries = await kit.EntriesAsync();

        Assert.Equal([(available.ItemId, _nine, (DateTime?)_nine.AddMinutes(90)), (onCall.ItemId, _nine.AddMinutes(90), null)], entries.Select(entry => (entry.StageId, entry.StartUtc, entry.EndUtc)));
        Assert.Equal(first.ItemId, entries[0].ItemId);
    }

    [Fact]
    public async Task ASyncThatOverridesAnotherClock_CutsItsTime_AndThatClocksNextSyncGoesAroundIt()
    {
        // The time clock says Ana works from 9:00; the contact center says she was on a call from 10:00 to 10:30.
        await using var kit = await Kit.CreateAsync();
        var work = await kit.StageAsync("Work");
        var onCall = await kit.StageAsync("On a call");

        kit.Now = _nine.AddHours(2);
        await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine });
        await kit.RangeSyncAsync(_nine.AddMinutes(50), [Kit.ClockId], new TimeEntry { StageId = onCall.ItemId, StartUtc = _nine.AddHours(1), EndUtc = _nine.AddMinutes(90) });
        var cut = await kit.EntriesAsync();

        await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine });
        var again = await kit.EntriesAsync();

        (string, DateTime, DateTime?, string)[] expected =
        [
            (work.ItemId, _nine, _nine.AddHours(1), Kit.ClockId),
            (onCall.ItemId, _nine.AddHours(1), _nine.AddMinutes(90), Kit.CallsId),
            (work.ItemId, _nine.AddMinutes(90), null, Kit.ClockId),
        ];

        Assert.Equal(expected, cut.Select(entry => (entry.StageId, entry.StartUtc, entry.EndUtc, entry.ManagedBy)));
        Assert.Equal(expected, again.Select(entry => (entry.StageId, entry.StartUtc, entry.EndUtc, entry.ManagedBy)));
    }

    [Fact]
    public async Task TimeKeptByAnotherClock_CantBeSwitchedChangedOrRemovedHere()
    {
        await using var kit = await Kit.CreateAsync(clockKeepsAna: true);
        var work = await kit.StageAsync("Work");

        kit.Now = _nine.AddHours(6);

        await kit.SyncAsync(new TimeEntry { StageId = work.ItemId, StartUtc = _nine, EndUtc = _nine.AddHours(4) });

        var entry = Assert.Single(await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false));
        var switched = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().SwitchAsync("ana", "ana", work.ItemId, TimeTrackerConstants.Sources.Self, "ana"));
        var changed = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().UpdateAsync(entry.ItemId, work.ItemId, _nine, _nine.AddHours(3), null, "manager", "Left early."));
        var removed = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().DeleteAsync(entry.ItemId, "manager", "Wrong day."));

        var clockOut = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ClockOutAsync("ana", "ana"));

        Assert.Equal("Your time is kept by the Time Clock.", Assert.Single(switched.Errors));
        Assert.Equal("Not clocked in.", Assert.Single(clockOut.Errors));
        Assert.Equal("This time comes from the Time Clock; correct it there.", Assert.Single(changed.Errors));
        Assert.Equal("This time comes from the Time Clock; correct it there.", Assert.Single(removed.Errors));
    }

    private sealed class Kit : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; private set; }

        public DateTime Now { get; set; } = _nine;

        public List<(TimeEntryChangeKind Kind, string Reason)> Changes { get; } = [];

        public const string ClockId = "Workforce.TimeClock";

        public const string CallsId = "Workforce.ContactCenter";

        public static async Task<Kit> CreateAsync(bool clockKeepsAna = false)
        {
            var kit = new Kit();
            var externalClock = new Mock<IExternalTimeClock>();

            externalClock.Setup(c => c.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string userId, CancellationToken _) => clockKeepsAna && userId == "ana" ? new ExternalTimeClockInfo { Id = ClockId, Name = "the Time Clock", Url = "/my-work" } : null);
            externalClock.Setup(c => c.FindByIdAsync(ClockId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalTimeClockInfo { Id = ClockId, Name = "the Time Clock", Url = "/my-work" });
            var clock = new Mock<global::OrchardCore.Modules.IClock>();
            var observer = new Mock<ITimeEntryObserver>();

            clock.SetupGet(c => c.UtcNow).Returns(() => kit.Now);
            observer.Setup(o => o.OnChangedAsync(It.IsAny<TimeEntryChangeKind>(), It.IsAny<TimeEntry>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((TimeEntryChangeKind kind, TimeEntry _, string _, string _, string reason, CancellationToken _) => kit.Changes.Add((kind, reason)))
                .Returns(Task.CompletedTask);

            kit.Harness = await CalendarTestHarness.CreateAsync(
                services =>
                {
                    services.AddTimeTrackerCore();
                    services.AddSingleton(clock.Object);
                    services.AddSingleton(observer.Object);
                    services.AddSingleton(externalClock.Object);
                },
                TimeTrackerIndexSchema.CreateAsync,
                [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
                [TimeTrackerConstants.CollectionName]);

            return kit;
        }

        public Task<TimeCategory> CategoryAsync(string name, decimal factor = 1)
            => Harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<TimeCategoryManager>();
                var category = await manager.NewAsync();

                category.Name = name;
                category.Factor = factor;
                Assert.True((await manager.ValidateAsync(category)).Succeeded);
                await manager.CreateAsync(category);

                return category;
            });

        public Task<TimeStage> StageAsync(string name, params TimeCategory[] categories)
            => Harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<TimeStageManager>();
                var stage = await manager.NewAsync();

                stage.Name = name;
                stage.CategoryIds = [.. categories.Select(category => category.ItemId)];
                await manager.CreateAsync(stage);

                return stage;
            });

        public async Task SwitchAsync(TimeStage stage, int minutesAfterNine)
        {
            Now = _nine.AddMinutes(minutesAfterNine);

            var result = await Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().SwitchAsync("ana", "ana", stage.ItemId, TimeTrackerConstants.Sources.Self, "ana"));

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        }

        public Task<TimeTrackerResult> SyncAsync(params TimeEntry[] desired)
            => Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().SyncAsync(new TimeEntrySync
            {
                UserId = "ana",
                UserName = "ana",
                ManagedBy = ClockId,
                Source = TimeTrackerConstants.Sources.Kiosk,
                FromUtc = _nine.AddDays(-1),
                ToUtc = _nine.AddDays(1),
                BuildAsync = _ => Task.FromResult<IReadOnlyList<TimeEntry>>(desired),
            }));

        // A sync that owns only its range, from a start to now, like the contact center's.
        public Task<TimeTrackerResult> RangeSyncAsync(DateTime fromUtc, string[] overrides, params TimeEntry[] desired)
            => Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().SyncAsync(new TimeEntrySync
            {
                UserId = "ana",
                UserName = "ana",
                ManagedBy = CallsId,
                Source = TimeTrackerConstants.Sources.ContactCenter,
                FromUtc = fromUtc,
                ToUtc = Now.AddMinutes(1),
                ReplacesRangeOnly = true,
                Overrides = overrides,
                BuildAsync = _ => Task.FromResult<IReadOnlyList<TimeEntry>>(desired),
            }));

        public Task<IReadOnlyList<TimeEntry>> EntriesAsync()
            => Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

        public async Task<List<string>> ManagedIdsAsync()
            => [.. (await Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false))
                .Where(entry => entry.ManagedBy == ClockId)
                .Select(entry => entry.ItemId)];

        public Task<IReadOnlyList<TimeTotalsRow>> TotalsAsync(bool byDay, DateTime? from = null, DateTime? to = null)
            => Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().GetTotalsAsync(new TimeEntryQuery { UserIds = ["ana"], FromUtc = from, ToUtc = to }, "America/New_York", byDay), commit: false);

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }
}
