using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Adherence against a published schedule: a worker's day from their agent's states, alerts once per episode, and
/// exceptions and agents linked to workers through their user.
/// </summary>
public sealed class AdherenceServiceTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task ADay_IsComputedFromThePublishedShiftAndTheAgentsStates()
    {
        await using var setup = await SetupAsync();

        setup.History.Spans.AddRange(
        [
            Span(AgentPresenceStatus.Available, 9, 0, 10, 0),
            Span(AgentPresenceStatus.Break, 10, 0, 10, 15),
            Span(AgentPresenceStatus.Available, 10, 15, 16, 30),
            Span(AgentPresenceStatus.Away, 16, 30, 17, 0),
        ]);

        var day = await setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().ComputeDayAsync(setup.Ana, _monday), commit: false);

        Assert.NotNull(day);
        Assert.Equal("America/New_York", day.TimeZoneId);
        Assert.Equal(8 * 3600, day.ScheduledSeconds);

        // The last half hour away from the phone is out of adherence; the end of a shift has no grace, since nothing
        // follows it.
        Assert.Equal((8 * 3600) - 1800, day.InAdherenceSeconds);
        Assert.Equal((7.75 * 3600) - 1800, day.ActualWorkSeconds);
    }

    [Fact]
    public async Task AnAgentOutTooLong_RaisesOneAlertPerEpisode()
    {
        await using var setup = await SetupAsync();

        setup.Agent.PresenceStatus = AgentPresenceStatus.Break;
        setup.Agent.PresenceChangedUtc = PlanningKit.Utc(_monday, new TimeOnly(9, 30));
        setup.Now = PlanningKit.Utc(_monday, new TimeOnly(9, 33));

        Assert.Equal(0, await RaiseAsync(setup));

        setup.Now = PlanningKit.Utc(_monday, new TimeOnly(9, 40));

        Assert.Equal(1, await RaiseAsync(setup));
        Assert.Equal(0, await RaiseAsync(setup));

        var raised = Assert.Single(setup.Events.Raised, raised => raised.Name == WorkforceEvents.OutOfAdherence);
        Assert.Equal(WorkforceEvents.OutOfAdherence, raised.Name);
        Assert.Equal(setup.Ana.ItemId, raised.Input["WorkerId"]);
        Assert.Equal(10, raised.Input["Minutes"]);

        // Back to work, then away again: a new episode.
        setup.Agent.PresenceStatus = AgentPresenceStatus.Away;
        setup.Agent.PresenceChangedUtc = PlanningKit.Utc(_monday, new TimeOnly(9, 45));
        setup.Now = PlanningKit.Utc(_monday, new TimeOnly(9, 55));

        Assert.Equal(1, await RaiseAsync(setup));
    }

    [Fact]
    public async Task AnException_IsLeftOutOfTheDay_AndSavingAgainReplacesTheDay()
    {
        await using var setup = await SetupAsync();

        setup.History.Spans.Add(Span(AgentPresenceStatus.Available, 9, 0, 16, 0));

        var errors = await setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().AddExceptionAsync(new AdherenceException
        {
            WorkerId = setup.Ana.ItemId,
            StartUtc = PlanningKit.Utc(_monday, new TimeOnly(16, 0)),
            EndUtc = PlanningKit.Utc(_monday, new TimeOnly(17, 0)),
            Reason = AdherenceExceptionReason.Coaching,
        }));

        Assert.Empty(errors);

        await setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().SaveDayAsync(setup.Ana, _monday));
        var day = await setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().SaveDayAsync(setup.Ana, _monday));

        Assert.Equal(3600, day.ExceptionSeconds);

        // On the phone through the break: 15 minutes out, less the grace at both ends of it.
        Assert.Equal((7 * 3600) - (11 * 60), day.InAdherenceSeconds);

        var saved = await setup.Harness.RunAsync(services => services.GetRequiredService<ISession>().QueryIndex<AdherenceDayIndex>(collection: WorkforceConstants.CollectionName).CountAsync(), commit: false);

        Assert.Equal(1, saved);
    }

    [Fact]
    public async Task TheRules_AreTheTeamsOrItsParents()
    {
        await using var setup = await SetupAsync();

        await setup.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AdherenceRuleSetManager>();
            var set = await manager.NewAsync();

            set.Name = "Strict";
            set.TeamIds = [setup.Team.ItemId];
            set.Rules.Single(rule => rule.Kind == ActivityKind.Work).AlertAfterMinutes = 1;
            await manager.CreateAsync(set);
        });

        var rules = await setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().RulesForTeamAsync(setup.Team.ItemId), commit: false);
        var defaults = await setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().RulesForTeamAsync("another-team"), commit: false);

        Assert.Equal(1, rules.Single(rule => rule.Kind == ActivityKind.Work).AlertAfterMinutes);
        Assert.Equal(5, defaults.Single(rule => rule.Kind == ActivityKind.Work).AlertAfterMinutes);
    }

    private static Task<int> RaiseAsync(Setup setup)
        => setup.Harness.RunAsync(services => services.GetRequiredService<AdherenceService>().RaiseAlertsAsync());

    private static AgentStateSpan Span(AgentPresenceStatus status, int startHour, int startMinute, int endHour, int endMinute)
        => new()
        {
            AgentId = "agent-ana",
            Status = status,
            StartUtc = PlanningKit.Utc(_monday, new TimeOnly(startHour, startMinute)),
            EndUtc = PlanningKit.Utc(_monday, new TimeOnly(endHour, endMinute)),
        };

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup { Now = CalendarTestHarness.Now };
        var agents = new Mock<IAgentProfileManager>();

        agents.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [setup.Agent]);
        agents.Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => userId == setup.Agent.UserId ? setup.Agent : null);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(() => setup.Now);

        setup.Harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton(agents.Object);
                services.AddSingleton<IAgentStateHistory>(setup.History);
                services.Replace(ServiceDescriptor.Singleton<IWorkforceEvents>(setup.Events));
                services.AddSingleton(clock.Object);
                services.AddScoped<AgentWorkerService>();
                services.AddScoped<AdherenceRuleSetStore>();
                services.AddScoped<AdherenceRuleSetManager>();
                services.AddScoped<ICatalogEntryHandler<AdherenceRuleSet>, AdherenceRuleSetHandler>();
                services.AddScoped<AdherenceService>();
            },
            AdherenceIndexSchema.CreateAsync,
            [new AdherenceRuleSetIndexProvider(), new AdherenceExceptionIndexProvider(), new AdherenceDayIndexProvider(), new AdherenceAlertIndexProvider()]);

        await setup.Harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());

        var breakCode = await setup.Harness.RunAsync(async services => (await services.GetRequiredService<IActivityCodeManager>().GetAllAsync()).Single(code => code.Kind == ActivityKind.Break), commit: false);

        setup.Team = await WorkforceTests.AddTeamAsync(setup.Harness, "Support", TeamKind.Team);
        setup.Ana = await PlanningKit.AddMemberAsync(setup.Harness, "Ana", setup.Team);
        setup.Agent = new AgentProfile { ItemId = "agent-ana", UserId = setup.Ana.UserId, UserName = "ana", PresenceStatus = AgentPresenceStatus.Available };

        var template = await PlanningKit.AddTemplateAsync(setup.Harness, "Day", new TimeOnly(9, 0), configure: template =>
            template.Segments = [new ShiftTemplateSegment { ActivityCodeId = breakCode.ItemId, OffsetMinutes = 60, LengthMinutes = 15 }]);
        var period = await PlanningKit.CreatePeriodAsync(setup.Harness, setup.Team);

        await setup.Harness.RunAsync(async services =>
        {
            var saved = await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(new DraftShift
            {
                ScenarioId = period.PrimaryScenarioId,
                WorkerId = setup.Ana.ItemId,
                Date = _monday,
                Start = _monday.ToDateTime(new TimeOnly(9, 0)),
                End = _monday.ToDateTime(new TimeOnly(17, 0)),
                TemplateId = template.ItemId,
                Segments = [new DraftShiftSegment { ActivityCodeId = breakCode.ItemId, Start = _monday.ToDateTime(new TimeOnly(10, 0)), End = _monday.ToDateTime(new TimeOnly(10, 15)) }],
            }, "tester");

            Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        });
        await PlanningKit.PublishAsync(setup.Harness, period);

        setup.Now = PlanningKit.Utc(_monday, new TimeOnly(20, 0));

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public FakeStateHistory History { get; } = new();

        public RecordingEvents Events { get; } = new();

        public WorkforceTeam Team { get; set; }

        public WorkerProfile Ana { get; set; }

        public AgentProfile Agent { get; set; }

        public DateTime Now { get; set; }

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private sealed class FakeStateHistory : IAgentStateHistory
    {
        public List<AgentStateSpan> Spans { get; } = [];

        public Task<IReadOnlyList<AgentStateSpan>> GetSpansAsync(IEnumerable<string> agentIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            var ids = agentIds.ToHashSet(StringComparer.Ordinal);

            return Task.FromResult<IReadOnlyList<AgentStateSpan>>([.. Spans.Where(span => ids.Contains(span.AgentId) && span.EndUtc > fromUtc && span.StartUtc < toUtc)]);
        }
    }

    private sealed class RecordingEvents : IWorkforceEvents
    {
        public List<(string Name, IDictionary<string, object> Input)> Raised { get; } = [];

        public Task RaiseAsync(string name, IDictionary<string, object> input, string correlationId, CancellationToken cancellationToken = default)
        {
            Raised.Add((name, input));

            return Task.CompletedTask;
        }
    }
}
