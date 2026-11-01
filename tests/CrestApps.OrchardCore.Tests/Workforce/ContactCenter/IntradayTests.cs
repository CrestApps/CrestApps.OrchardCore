using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Intraday management: how far the day runs from its forecast, the quietest times for a meeting, and voluntary
/// overtime and time off that lengthen or shorten a published shift as the worker's own change.
/// </summary>
public sealed class IntradayTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;
    private static readonly DateTime _nine = PlanningKit.Utc(_monday, new TimeOnly(9, 0));

    [Fact]
    public void TheReforecastFactor_FollowsHalfTheDifferenceSoFar()
    {
        var rows = new[]
        {
            new IntradayRow { StartUtc = _nine, Elapsed = true, ForecastVolume = 10, ActualVolume = 14 },
            new IntradayRow { StartUtc = _nine.AddMinutes(15), Elapsed = true, ForecastVolume = 10, ActualVolume = 16 },
            new IntradayRow { StartUtc = _nine.AddMinutes(30), Elapsed = false, ForecastVolume = 10 },
        };

        // 30 against 20 is 1.5; half of the difference is 1.25.
        Assert.Equal(1.25, IntradayCalculator.ReforecastFactor(rows), 6);
        Assert.Equal(1, IntradayCalculator.ReforecastFactor([new IntradayRow { StartUtc = _nine, ForecastVolume = 10 }]));
    }

    [Fact]
    public void TheQuietestTimes_AddTheLeastShortfall()
    {
        var rows = Enumerable.Range(0, 8).Select(slot => new IntradayRow
        {
            StartUtc = _nine.AddMinutes(15 * slot),
            Required = slot < 4 ? 5 : 2,
            Scheduled = 5,
        }).ToArray();

        var windows = IntradayCalculator.FindWindows(rows, people: 2, minutes: 30, fromUtc: _nine);

        // Taking 2 of 5 when 2 are needed costs nothing; when 5 are needed it costs 2 an interval.
        Assert.Equal(_nine.AddMinutes(60), windows[0].StartUtc);
        Assert.Equal(_nine.AddMinutes(90), windows[0].EndUtc);
        Assert.Equal(0, windows[0].AddedShortfall);
        Assert.Empty(IntradayCalculator.FindWindows(rows, people: 6, minutes: 30, fromUtc: _nine));
    }

    [Fact]
    public async Task Overtime_LengthensTheShift_AsTheWorkersChange()
    {
        await using var setup = await SetupAsync();

        var offer = await OfferAsync(setup, IntradayOfferKind.VoluntaryOvertime, new TimeOnly(17, 0), new TimeOnly(18, 0), places: 1);

        Assert.Empty(await TakeAsync(setup, offer, setup.Ana));

        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(setup.Harness, setup.Team));
        Assert.Equal(PlanningKit.Utc(_monday, new TimeOnly(18, 0)), shift.EndUtc);

        var change = await LastChangeAsync(setup);
        Assert.Equal(ScheduleChangeKind.Moved, change.Kind);
        Assert.Equal(ScheduleChangeInitiator.Worker, change.InitiatedBy);
        Assert.Equal(PlanningKit.Utc(_monday, new TimeOnly(17, 0)), change.BeforeEndUtc);

        var stored = await setup.Harness.RunAsync(services => services.GetRequiredService<IntradayService>().FindAsync(offer.ItemId), commit: false);
        Assert.Equal(IntradayOfferStatus.Filled, stored.Status);
        Assert.Single(stored.Claims);
    }

    [Fact]
    public async Task TimeOff_ShortensTheShift_AndOffersNextToNoShiftAreRefused()
    {
        await using var setup = await SetupAsync();

        var timeOff = await OfferAsync(setup, IntradayOfferKind.VoluntaryTimeOff, new TimeOnly(16, 0), new TimeOnly(17, 0), places: 2);
        var lonely = await OfferAsync(setup, IntradayOfferKind.VoluntaryOvertime, new TimeOnly(19, 0), new TimeOnly(20, 0), places: 1);

        Assert.Empty(await TakeAsync(setup, timeOff, setup.Ana));
        Assert.NotEmpty(await TakeAsync(setup, timeOff, setup.Ana));
        Assert.NotEmpty(await TakeAsync(setup, lonely, setup.Ana));

        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(setup.Harness, setup.Team));
        Assert.Equal(PlanningKit.Utc(_monday, new TimeOnly(16, 0)), shift.EndUtc);
    }

    private static async Task<IntradayOffer> OfferAsync(Setup setup, IntradayOfferKind kind, TimeOnly start, TimeOnly end, int places)
    {
        var offer = new IntradayOffer
        {
            PlanningGroupId = setup.Group.ItemId,
            Kind = kind,
            StartUtc = PlanningKit.Utc(_monday, start),
            EndUtc = PlanningKit.Utc(_monday, end),
            Places = places,
        };

        Assert.Empty(await setup.Harness.RunAsync(services => services.GetRequiredService<IntradayService>().OfferAsync(offer)));

        return offer;
    }

    private static Task<IReadOnlyList<string>> TakeAsync(Setup setup, IntradayOffer offer, WorkerProfile worker)
        => setup.Harness.RunAsync(services => services.GetRequiredService<IntradayService>().TakeAsync(offer.ItemId, worker.ItemId, "ana"));

    private static Task<ScheduleChange> LastChangeAsync(Setup setup)
        => setup.Harness.RunAsync(async services => (await services.GetRequiredService<ISession>().Query<ScheduleChange, ScheduleChangeIndex>(collection: WorkforceConstants.CollectionName).ListAsync()).OrderBy(change => change.ChangedUtc).Last(), commit: false);

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        var clock = new Mock<IClock>();

        clock.SetupGet(c => c.UtcNow).Returns(() => setup.Now);

        setup.Harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton(clock.Object);
                services.AddSingleton(new Mock<IIntervalStatisticsReader>().Object);
                services.AddScoped<PlanningGroupStore>();
                services.AddScoped<PlanningGroupManager>();
                services.AddScoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>();
                services.AddScoped<ForecastEventStore>();
                services.AddScoped<ForecastEventManager>();
                services.AddScoped<ForecastService>();
                services.AddScoped<IntradayService>();
            },
            async schemaBuilder =>
            {
                await ForecastingIndexSchema.CreateAsync(schemaBuilder);
                await IntradayIndexSchema.CreateAsync(schemaBuilder);
            },
            [new PlanningGroupIndexProvider(), new ForecastEventIndexProvider(), new ForecastIndexProvider(), new IntradayOfferIndexProvider()]);

        setup.Team = await WorkforceTests.AddTeamAsync(setup.Harness, "Sales", TeamKind.Team);
        setup.Ana = await PlanningKit.AddMemberAsync(setup.Harness, "Ana", setup.Team);
        setup.Group = await setup.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<PlanningGroupManager>();
            var group = await manager.NewAsync();

            group.Name = "Sales voice";
            group.QueueIds = ["sales"];
            group.TeamId = setup.Team.ItemId;
            await manager.CreateAsync(group);

            return group;
        });

        var period = await PlanningKit.CreatePeriodAsync(setup.Harness, setup.Team);

        await PlanningKit.AddDraftAsync(setup.Harness, period, setup.Ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(setup.Harness, period);

        setup.Now = PlanningKit.Utc(_monday, new TimeOnly(8, 0));

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public Calendars.CalendarTestHarness Harness { get; set; }

        public WorkforceTeam Team { get; set; }

        public WorkerProfile Ana { get; set; }

        public PlanningGroup Group { get; set; }

        public DateTime Now { get; set; } = Calendars.CalendarTestHarness.Now;

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
