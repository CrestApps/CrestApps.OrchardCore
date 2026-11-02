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

    private sealed class Kit : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; private set; }

        public DateTime Now { get; set; } = _nine;

        public List<(TimeEntryChangeKind Kind, string Reason)> Changes { get; } = [];

        public static async Task<Kit> CreateAsync()
        {
            var kit = new Kit();
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

        public Task<IReadOnlyList<TimeTotalsRow>> TotalsAsync(bool byDay, DateTime? from = null, DateTime? to = null)
            => Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().GetTotalsAsync(new TimeEntryQuery { UserIds = ["ana"], FromUtc = from, ToUtc = to }, "America/New_York", byDay), commit: false);

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }
}
