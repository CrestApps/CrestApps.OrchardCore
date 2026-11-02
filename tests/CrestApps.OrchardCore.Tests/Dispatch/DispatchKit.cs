using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// A field team on shift on Monday 9 November 2026, 8:00 to 16:00 in New York, with work orders and their visits.
/// </summary>
internal sealed class DispatchKit : IAsyncDisposable
{
    // Downtown Boston, and a customer 20 km south, which is 20 minutes at 60 km/h.
    public static readonly GeoPoint Depot = new(42.3601, -71.0589);

    public static readonly GeoPoint South = new(42.1803, -71.0589);

    public CalendarTestHarness Harness { get; private set; }

    public DispatchSettings Settings { get; } = new() { BufferMinutes = 10 };

    public WorkforceTeam Team { get; private set; }

    public WorkOrderType Type { get; private set; }

    public static DateTime Monday(int hour, int minute = 0) => PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(hour, minute));

    public static async Task<DispatchKit> CreateAsync(bool realLocks = false, TimeSpan? travelDelay = null)
    {
        var kit = new DispatchKit();
        var settings = new Mock<IDispatchSettingsProvider>();
        var geo = new Mock<IGeoService>();

        settings.Setup(provider => provider.GetAsync()).ReturnsAsync(() => kit.Settings);

        // Straight lines at 60 km/h: a kilometer a minute.
        geo.Setup(service => service.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime _, TravelOptions _, CancellationToken _) =>
            {
                if (travelDelay is not null)
                {
                    await Task.Delay(travelDelay.Value, CancellationToken.None);
                }

                return Matrix(origins, destinations);
            });

        kit.Harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddWorkOrdersCore();
                services.AddDispatchCore();
                services.TryAddScoped<ServiceTerritoryStore>();
                services.AddSingleton(settings.Object);
                services.AddSingleton(geo.Object);

                if (realLocks)
                {
                    services.AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance));
                }
            },
            async schema =>
            {
                await WorkOrdersIndexSchema.CreateAsync(schema);
                await DispatchIndexSchema.CreateAsync(schema);
                await GeoIndexSchema.CreateAsync(schema);
            },
            new IIndexProvider[]
            {
                new WorkOrderIndexProvider(), new WorkOrderTypeIndexProvider(), new ChecklistTemplateIndexProvider(), new ServiceLevelPolicyIndexProvider(), new WorkOrderSubStatusIndexProvider(),
                new ServiceVisitIndexProvider(),
                new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider(),
            },
            [WorkOrdersConstants.CollectionName, DispatchConstants.CollectionName, GeoConstants.CollectionName]);

        kit.Team = await WorkforceTests.AddTeamAsync(kit.Harness, "Field", TeamKind.Team);
        kit.Team = await PlanningKit.UpdateTeamAsync(kit.Harness, kit.Team, team => team.Location = new WorkforceLocation { Latitude = Depot.Latitude, Longitude = Depot.Longitude });
        kit.Type = await kit.Harness.RunAsync(async services =>
        {
            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.NewAsync();

            type.Name = "Furnace repair";
            type.DefaultDurationMinutes = 60;
            await types.CreateAsync(type);

            return type;
        });

        return kit;
    }

    private static TravelMatrix Matrix(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations) => new()
    {
        Provider = "Test",
        Legs = origins.Select(origin => (IReadOnlyList<TravelLeg>)destinations.Select(destination =>
        {
            var leg = HaversineTravelTimeProvider.Leg(origin, destination, 1, 60);

            return new TravelLeg(TimeSpan.FromMinutes(Math.Round(leg.Duration.TotalMinutes)), leg.DistanceMeters);
        }).ToArray()).ToArray(),
    };

    public Task<WorkerProfile> AddTechnicianAsync(string name, Action<WorkerProfile> configure = null)
        => PlanningKit.AddMemberAsync(Harness, name, Team, configure);

    public async Task PublishShiftsAsync(params WorkerProfile[] workers)
    {
        var period = await PlanningKit.CreatePeriodAsync(Harness, Team);

        foreach (var worker in workers)
        {
            await PlanningKit.AddDraftAsync(Harness, period, worker.ItemId, PlanningKit.Monday, new TimeOnly(8, 0), new TimeOnly(16, 0));
        }

        await PlanningKit.PublishAsync(Harness, period);
    }

    public Task<WorkOrderType> UpdateTypeAsync(Action<WorkOrderType> change)
        => Harness.RunAsync(async services =>
        {
            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.FindByIdAsync(Type.ItemId);

            change(type);
            await types.UpdateAsync(type);
            Type = type;

            return type;
        });

    // Makes a work order at a place; its visit is made with it.
    public async Task<(WorkOrder WorkOrder, ServiceVisit Visit)> AddJobAsync(string summary, GeoPoint place, Action<WorkOrder> configure = null)
    {
        var workOrder = await Harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<WorkOrderService>();
            var workOrder = await service.NewAsync(Type.ItemId);

            workOrder.Summary = summary;
            workOrder.ServiceAddress = new Address { AddressLine1 = summary, City = "Boston", Latitude = place.Latitude, Longitude = place.Longitude };
            configure?.Invoke(workOrder);

            var result = await service.CreateAsync(workOrder, "dispatcher");

            Assert.Empty(result.Errors);

            return result.WorkOrder;
        });

        var visit = Assert.Single(await VisitsOfAsync(workOrder.ItemId));

        return (workOrder, visit);
    }

    public Task<IReadOnlyList<ServiceVisit>> VisitsOfAsync(string workOrderId)
        => Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().ListForWorkOrderAsync(workOrderId), commit: false);

    public Task<DispatchResult> AssignAsync(ServiceVisit visit, DateTime startUtc, params WorkerProfile[] workers)
        => Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().AssignAsync(visit.ItemId, new VisitAssignment { WorkerIds = workers.Select(worker => worker.ItemId).ToList(), StartUtc = startUtc }, "dispatcher"));

    public Task<DispatchResult> MoveAsync(ServiceVisit visit, VisitStatus status, string note = null)
        => Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().ChangeStatusAsync(visit.ItemId, status, note, "technician"));

    public Task<DispatchCandidateResult> CandidatesAsync(ServiceVisit visit)
        => Harness.RunAsync(services => services.GetRequiredService<IDispatchCandidateFinder>().FindAsync(visit, Monday(0), Monday(23, 59)), commit: false);

    public Task<WorkOrder> WorkOrderAsync(string id)
        => Harness.RunAsync(async services => await services.GetRequiredService<WorkOrderService>().FindAsync(id), commit: false);

    public async Task<IReadOnlyList<CalendarEvent>> JobEventsAsync()
    {
        var calendarId = await Harness.RunAsync(async services => (await services.GetRequiredService<CrestApps.OrchardCore.Workforce.Services.ITeamManager>().FindByIdAsync(Team.ItemId)).CalendarId, commit: false);
        var occurrences = await Harness.GetOccurrencesAsync(new CrestApps.OrchardCore.Calendars.Services.CalendarOccurrenceQuery
        {
            CalendarIds = [calendarId],
            FromUtc = Monday(0).AddDays(-1),
            ToUtc = Monday(0).AddDays(2),
            EventTypes = [DispatchConstants.JobEventType],
        });

        return occurrences.Select(occurrence => occurrence.Event).Where(calendarEvent => calendarEvent.Status != CalendarEventStatus.Cancelled).ToArray();
    }

    public static string WorkerOf(CalendarEvent calendarEvent)
        => calendarEvent.TryGet<VisitEventData>(out var data) ? data.WorkerId : null;

    public ValueTask DisposeAsync() => Harness.DisposeAsync();
}
