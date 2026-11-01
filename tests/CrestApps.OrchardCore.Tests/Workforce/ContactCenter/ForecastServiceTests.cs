using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// A planning group's forecast from its queues' interval statistics: the volume follows the history in the team's
/// time zone, each interval is staffed with shrinkage, an adjustment staffs its days again, and the active forecast
/// becomes the team's coverage.
/// </summary>
public sealed class ForecastServiceTests
{
    private const string Zone = "America/New_York";
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task AForecast_FollowsTheHistory_AndIsStaffed()
    {
        await using var setup = await SetupAsync();

        var created = await setup.Harness.RunAsync(services => services.GetRequiredService<ForecastService>().CreateAsync(setup.Group.ItemId, _monday, _monday.AddDays(6), ForecastModelKind.Auto, "planner"));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));

        var forecast = created.Value;
        var nine = forecast.Intervals.Single(interval => interval.Date == _monday && interval.Slot == 36);
        var one = forecast.Intervals.Single(interval => interval.Date == _monday && interval.Slot == 52);

        Assert.Equal(ForecastStatus.Draft, forecast.Status);
        Assert.Equal(Zone, forecast.TimeZoneId);
        Assert.Equal(CalendarTime.ToUtc(_monday.ToDateTime(new TimeOnly(9, 0)), Zone), nine.StartUtc);
        Assert.Equal(10, nine.Volume, 6);
        Assert.Equal(20, one.Volume, 6);
        Assert.Equal(300, one.AverageHandleSeconds, 6);
        Assert.True(one.ServiceLevel >= 0.8);
        Assert.Equal((int)Math.Ceiling(one.RequiredAgents / 0.7), one.RequiredScheduled);
        Assert.DoesNotContain(forecast.Intervals, interval => interval.Date.DayOfWeek == DayOfWeek.Saturday);
        Assert.NotEmpty(forecast.Backtests);
    }

    [Fact]
    public async Task TheActiveForecast_IsTheTeamsCoverage_AndAnAdjustmentChangesIt()
    {
        await using var setup = await SetupAsync();

        var forecast = (await setup.Harness.RunAsync(services => services.GetRequiredService<ForecastService>().CreateAsync(setup.Group.ItemId, _monday, _monday.AddDays(6), ForecastModelKind.WeightedHistory, "planner"))).Value;

        Assert.Empty(await setup.Harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<ForecastService>();

            return await service.ActivateAsync(await service.FindAsync(forecast.ItemId));
        }));

        var requirement = await CoverageAsync(setup);
        var monday = requirement.Rows.Where(row => row.Date == _monday).OrderBy(row => row.Start).ToArray();
        var nine = forecast.Intervals.Single(interval => interval.Date == _monday && interval.Slot == 36);

        Assert.Equal(CoverageSource.Forecast, requirement.Source);
        Assert.Equal(_monday, requirement.EffectiveFrom);
        Assert.Equal(new TimeOnly(9, 0), monday[0].Start);
        Assert.Equal(new TimeOnly(12, 0), monday[0].End);
        Assert.Equal(nine.RequiredAgents, monday[0].Minimum);
        Assert.Equal(nine.RequiredScheduled, monday[0].Target);
        Assert.Equal(new TimeOnly(17, 0), monday[^1].End);

        var errors = await setup.Harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<ForecastService>();

            return await service.AdjustAsync(await service.FindAsync(forecast.ItemId), new ForecastAdjustment { FromDate = _monday, ToDate = _monday, Percent = 100, Note = "Launch" });
        });

        Assert.Empty(errors);

        var adjusted = await setup.Harness.RunAsync(services => services.GetRequiredService<ForecastService>().FindAsync(forecast.ItemId), commit: false);
        var busier = (await CoverageAsync(setup)).Rows.Where(row => row.Date == _monday).OrderBy(row => row.Start).First();

        Assert.Equal(20, adjusted.Intervals.Single(interval => interval.Date == _monday && interval.Slot == 36).Volume, 6);
        Assert.True(busier.Minimum > monday[0].Minimum);
    }

    [Fact]
    public async Task AGroupWithoutHistory_CantBeForecast()
    {
        await using var setup = await SetupAsync(history: false);

        var created = await setup.Harness.RunAsync(services => services.GetRequiredService<ForecastService>().CreateAsync(setup.Group.ItemId, _monday, _monday.AddDays(6), ForecastModelKind.Auto, "planner"));

        Assert.False(created.Succeeded);
    }

    private static async Task<CoverageRequirement> CoverageAsync(Setup setup)
        => (await setup.Harness.RunAsync(services => services.GetRequiredService<ICoverageService>().ListRequirementsAsync(setup.Team.ItemId), commit: false))
            .Single(item => item.Source == CoverageSource.Forecast);

    private static async Task<Setup> SetupAsync(bool history = true)
    {
        var setup = new Setup();
        var reader = new FakeStatistics();

        if (history)
        {
            for (var day = -56; day < 0; day++)
            {
                var date = _monday.AddDays(day);

                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    continue;
                }

                for (var slot = 36; slot < 68; slot++)
                {
                    var volume = slot < 48 ? 10 : 20;

                    reader.Intervals.Add(new QueueIntervalStatistic
                    {
                        QueueId = "sales",
                        Channel = InteractionChannel.Voice,
                        IntervalStartUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue).AddMinutes(slot * 15), Zone),
                        Offered = volume,
                        Answered = volume,
                        Handled = volume,
                        TalkSeconds = volume * 270,
                        WrapUpSeconds = volume * 30,
                    });
                }
            }
        }

        setup.Harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton<IIntervalStatisticsReader>(reader);
                services.AddScoped<PlanningGroupStore>();
                services.AddScoped<PlanningGroupManager>();
                services.AddScoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>();
                services.AddScoped<ForecastEventStore>();
                services.AddScoped<ForecastEventManager>();
                services.AddScoped<ICatalogEntryHandler<ForecastEvent>, ForecastEventHandler>();
                services.AddScoped<ForecastService>();
            },
            ForecastingIndexSchema.CreateAsync,
            [new PlanningGroupIndexProvider(), new ForecastEventIndexProvider(), new ForecastIndexProvider()]);

        setup.Team = await WorkforceTests.AddTeamAsync(setup.Harness, "Sales", TeamKind.Team);
        setup.Group = await setup.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<PlanningGroupManager>();
            var group = await manager.NewAsync();

            group.Name = "Sales voice";
            group.QueueIds = ["sales"];
            group.TeamId = setup.Team.ItemId;

            var validation = await manager.ValidateAsync(group);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(group);

            return group;
        });

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public Calendars.CalendarTestHarness Harness { get; set; }

        public WorkforceTeam Team { get; set; }

        public PlanningGroup Group { get; set; }

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private sealed class FakeStatistics : IIntervalStatisticsReader
    {
        public List<QueueIntervalStatistic> Intervals { get; } = [];

        public Task<IReadOnlyList<QueueIntervalStatistic>> GetQueueIntervalsAsync(IEnumerable<string> queueIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            var ids = queueIds?.ToHashSet(StringComparer.Ordinal);

            return Task.FromResult<IReadOnlyList<QueueIntervalStatistic>>([.. Intervals.Where(interval => (ids is null || ids.Contains(interval.QueueId)) && interval.IntervalStartUtc >= fromUtc && interval.IntervalStartUtc < toUtc)]);
        }

        public Task<IReadOnlyList<AgentIntervalStatistic>> GetAgentIntervalsAsync(IEnumerable<string> userIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentIntervalStatistic>>([]);

        public Task<IReadOnlyList<IntervalStatisticsDay>> GetDaysAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IntervalStatisticsDay>>([]);
    }
}
