using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter;
using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Agent states in Time Tracker: each state, or a break's reason, is kept in its stage while the agent works, over
/// the time clock's work.
/// </summary>
public sealed class ContactCenterTimeTrackerTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task AgentStates_AreKeptInTheirStages_ABreakReasonInItsOwn_AndTheCurrentStateStaysOpen()
    {
        // Arrange (Available 9:00, a call 9:10, wrap-up 9:20, available 9:25, lunch break 12:00, a short break 13:00)
        await using var kit = await Kit.CreateAsync();

        kit.Spans.AddRange(
        [
            Span(AgentPresenceStatus.Available, 9, 0, 9, 10),
            Span(AgentPresenceStatus.Reserved, 9, 10, 9, 10, seconds: 20),
            Span(AgentPresenceStatus.Busy, 9, 10, 9, 20, startSeconds: 20),
            Span(AgentPresenceStatus.WrapUp, 9, 20, 9, 25),
            Span(AgentPresenceStatus.Available, 9, 25, 12, 0),
            Span(AgentPresenceStatus.Break, 12, 0, 13, 0, reason: "Lunch"),
            Span(AgentPresenceStatus.Break, 13, 0, 13, 15, reason: "Short break"),
            Span(AgentPresenceStatus.DoNotDisturb, 13, 15, 13, 30),
        ]);
        kit.Now = TimeClockKit.Local(_monday, 13, 30);

        // Act
        await kit.SyncAsync(TimeClockKit.Local(_monday, 8, 50));
        var entries = await kit.EntriesAsync();

        // Assert (Reserved counts as available; Do not disturb has no stage, so the agent's time stops there)
        Assert.Equal(
            ["Available 9:00-9:10:20", "On a call 9:10:20-9:20", "Wrap-up 9:20-9:25", "Available 9:25-12:00", "Lunch 12:00-13:00", "Break 13:00-13:15"],
            entries.Select(entry => kit.Describe(entry)));
        Assert.All(entries, entry => Assert.Equal(WorkforceContactCenterConstants.TimeTrackerClockId, entry.ManagedBy));
        Assert.All(entries, entry => Assert.Equal(TimeTrackerConstants.Sources.ContactCenter, entry.Source));
    }

    [Fact]
    public async Task EachStateChange_SyncsOnlyTheLastMinutes_AndTheStateTheAgentIsInStaysOpen()
    {
        await using var kit = await Kit.CreateAsync();

        kit.Spans.Add(Span(AgentPresenceStatus.Available, 9, 0, 9, 30));
        kit.Now = TimeClockKit.Local(_monday, 9, 30);
        await kit.SyncAsync(TimeClockKit.Local(_monday, 8, 50));

        // At 10:30 she takes a call; the log is read from 10:20, so the history starts there.
        kit.Spans.Clear();
        kit.Spans.Add(Span(AgentPresenceStatus.Available, 10, 20, 10, 30));
        kit.Spans.Add(Span(AgentPresenceStatus.Busy, 10, 30, 10, 31));
        kit.Now = TimeClockKit.Local(_monday, 10, 31);
        await kit.SyncAsync(TimeClockKit.Local(_monday, 10, 20));

        var entries = await kit.EntriesAsync();

        Assert.Equal(["Available 9:00-10:30", "On a call 10:30-now"], entries.Select(entry => kit.Describe(entry)));
    }

    [Fact]
    public async Task ForAWorkerOnTheTimeClock_TheStatesReplaceTheirWork_WhileTheyAreSignedIn()
    {
        await using var kit = await Kit.CreateAsync();
        var team = await kit.Clock.AddTeamAsync("Support");
        var ana = await PlanningKit.AddMemberAsync(kit.Clock.Harness, "Ana", team, worker => worker.UserId = "ana");

        await kit.Clock.PunchAtAsync(ana, TimePunchKind.In, _monday, 9);

        kit.Spans.Add(Span(AgentPresenceStatus.Available, 9, 30, 10, 0));
        kit.Spans.Add(Span(AgentPresenceStatus.Busy, 10, 0, 10, 15));
        kit.Spans.Add(Span(AgentPresenceStatus.Offline, 10, 15, 10, 30));
        kit.Now = TimeClockKit.Local(_monday, 10, 30);
        await kit.SyncAsync(TimeClockKit.Local(_monday, 9, 20));

        var entries = await kit.EntriesAsync();

        Assert.Equal(["Work 9:00-9:30", "Available 9:30-10:00", "On a call 10:00-10:15", "Work 10:15-now"], entries.Select(entry => kit.Describe(entry)));
    }

    [Fact]
    public async Task TheSeeder_GivesTheUsualStatesAStage_Once_AndKeepsWhatIsAlreadySet()
    {
        await using var kit = await Kit.CreateAsync(seed: false);

        var first = await kit.Clock.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<ContactCenterTimeStageSeeder>(services).BuildAsync(new ContactCenterTimeStages()));
        var kept = await kit.Clock.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<ContactCenterTimeStageSeeder>(services).BuildAsync(new ContactCenterTimeStages { States = { [nameof(AgentPresenceStatus.Busy)] = "mine" } }));
        var stages = await kit.Clock.Harness.RunAsync(async services => (await services.GetRequiredService<ITimeStageManager>().GetAllAsync()).ToList(), commit: false);

        Assert.Equal(
            [nameof(AgentPresenceStatus.Available), nameof(AgentPresenceStatus.Reserved), nameof(AgentPresenceStatus.RequestBreak), nameof(AgentPresenceStatus.Busy), nameof(AgentPresenceStatus.WrapUp), nameof(AgentPresenceStatus.Break), nameof(AgentPresenceStatus.Away), nameof(AgentPresenceStatus.Meeting), nameof(AgentPresenceStatus.Training)],
            first.States.Keys);
        Assert.Equal(first.States[nameof(AgentPresenceStatus.Available)], first.States[nameof(AgentPresenceStatus.Reserved)]);
        Assert.Equal("mine", kept.States[nameof(AgentPresenceStatus.Busy)]);
        Assert.Equal(stages.Count, stages.Select(stage => stage.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(stages, stage => stage.Name == "On a call");
    }

    private static AgentStateSpan Span(AgentPresenceStatus status, int fromHour, int fromMinute, int toHour, int toMinute, string reason = null, int seconds = 0, int startSeconds = 0)
        => new()
        {
            AgentId = "agent-ana",
            Status = status,
            Reason = reason,
            StartUtc = TimeClockKit.Local(_monday, fromHour, fromMinute).AddSeconds(startSeconds),
            EndUtc = TimeClockKit.Local(_monday, toHour, toMinute).AddSeconds(seconds),
        };

    private sealed class Kit : IAsyncDisposable
    {
        private Dictionary<string, string> _stageNames;

        public TimeClockKit Clock { get; private set; }

        public List<AgentStateSpan> Spans { get; } = [];

        public DateTime Now
        {
            get => Clock.Now;
            set => Clock.Now = value;
        }

        public static async Task<Kit> CreateAsync(bool seed = true)
        {
            var kit = new Kit();
            var history = new Mock<IAgentStateHistory>();
            var site = new SiteSettings();
            var siteService = new Mock<ISiteService>();

            history.Setup(h => h.GetSpansAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<string> _, DateTime from, DateTime to, CancellationToken _) => [.. kit.Spans.Where(span => span.EndUtc > from && span.StartUtc < to)]);
            siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(() => site);

            kit.Clock = await TimeClockKit.CreateAsync(
                services =>
                {
                    services.AddTimeTrackerCore();
                    services.AddWorkforceTimeClockTimeTracker();
                    services.AddWorkforceContactCenterTimeTracker();
                    services.AddSingleton(history.Object);
                    services.AddSingleton(siteService.Object);
                },
                TimeTrackerIndexSchema.CreateAsync,
                [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
                [TimeTrackerConstants.CollectionName]);

            await kit.Clock.Harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());
            await kit.Clock.Harness.RunAsync(services => services.GetRequiredService<TimeClockStageSeeder>().SeedAsync());

            if (seed)
            {
                var stages = await kit.Clock.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<ContactCenterTimeStageSeeder>(services).BuildAsync(new ContactCenterTimeStages()));
                var lunch = await kit.Clock.Harness.RunAsync(async services =>
                {
                    var manager = services.GetRequiredService<ITimeStageManager>();
                    var stage = await manager.NewAsync();

                    stage.Name = "Lunch";
                    await manager.CreateAsync(stage);

                    return stage;
                });

                stages.Reasons["Lunch"] = lunch.ItemId;
                site.Put(stages);
            }

            return kit;
        }

        public Task SyncAsync(DateTime fromUtc)
            => Clock.Harness.RunAsync(async services =>
            {
                var result = await services.GetRequiredService<ContactCenterTimeTrackerSync>().SyncAsync("agent-ana", "ana", "ana", fromUtc);

                Assert.True(result.Succeeded, string.Join(" ", result.Errors));
            });

        public Task<IReadOnlyList<TimeEntry>> EntriesAsync()
            => Clock.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

        public string Describe(TimeEntry entry)
        {
            _stageNames ??= Clock.Harness.RunAsync(async services => (await services.GetRequiredService<TimeStageManager>().GetAllAsync()).ToDictionary(stage => stage.ItemId, stage => stage.Name), commit: false).GetAwaiter().GetResult();

            return $"{_stageNames[entry.StageId]} {Time(entry.StartUtc)}-{(entry.EndUtc is { } end ? Time(end) : "now")}";
        }

        public ValueTask DisposeAsync() => Clock.DisposeAsync();

        private static string Time(DateTime utc)
        {
            var local = global::CrestApps.OrchardCore.Calendars.CalendarTime.ToLocal(utc, "America/New_York");

            return local.Second == 0 ? $"{local.Hour}:{local.Minute:00}" : $"{local.Hour}:{local.Minute:00}:{local.Second:00}";
        }
    }
}
