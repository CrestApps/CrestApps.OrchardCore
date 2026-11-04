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
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Calendars;
using Microsoft.AspNetCore.DataProtection;
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

    public DateTime Now { get; set; } = CalendarTestHarness.Now;

    public List<VisitNoticeMessage> Sent { get; } = [];

    public List<(string VisitId, string EventName)> Events { get; } = [];

    public List<DispatchEquipment> Equipment { get; } = [];

    public MemoryFieldFileStore Files { get; } = new();

    public Team Team { get; private set; }

    public WorkOrderType Type { get; private set; }

    public static DateTime Monday(int hour, int minute = 0) => PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(hour, minute));

    public static async Task<DispatchKit> CreateAsync(bool realLocks = false, TimeSpan? travelDelay = null)
    {
        var kit = new DispatchKit();
        var settings = new Mock<IDispatchSettingsProvider>();
        var geo = new Mock<IGeoService>();

        settings.Setup(provider => provider.GetAsync()).ReturnsAsync(() => kit.Settings);
        settings.Setup(provider => provider.GetTimeZoneIdAsync()).ReturnsAsync(PlanningKit.Zone);

        var clock = new Mock<global::OrchardCore.Modules.IClock>();

        clock.SetupGet(c => c.UtcNow).Returns(() => kit.Now);

        var sender = new Mock<IVisitNoticeSender>();

        sender.SetupGet(s => s.Channel).Returns("Sms");
        sender.Setup(s => s.CanSend(It.IsAny<VisitNoticeMessage>())).Returns((VisitNoticeMessage message) => !string.IsNullOrEmpty(message.Phone));
        sender.Setup(s => s.SendAsync(It.IsAny<VisitNoticeMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VisitNoticeMessage message, CancellationToken _) =>
            {
                kit.Sent.Add(message);

                return null;
            });

        var site = new Mock<IDispatchSiteInfo>();

        site.Setup(s => s.CompanyNameAsync()).ReturnsAsync("Acme Heating");
        site.Setup(s => s.TrackingUrlAsync(It.IsAny<string>())).ReturnsAsync((string token) => "https://example.test/track/" + token);

        var observer = new Mock<IVisitObserver>();

        observer.Setup(o => o.OnEventAsync(It.IsAny<ServiceVisit>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback((ServiceVisit visit, string name, IReadOnlyDictionary<string, object> _, CancellationToken _) => kit.Events.Add((visit.ItemId, name)))
            .Returns(Task.CompletedTask);

        var equipment = new Mock<IDispatchEquipmentService>();

        equipment.Setup(e => e.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string typeId, CancellationToken _) => kit.Equipment.Where(item => item.AssetId.StartsWith(typeId, StringComparison.Ordinal)).ToList());
        equipment.Setup(e => e.TypeNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string typeId, CancellationToken _) => typeId);

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
                services.AddDispatchMobileCore();
                services.AddAssetsCore();
                services.AddSingleton<IFieldFileStore>(kit.Files);
                services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.HttpContextAccessor>();
                services.AddScoped<CrestApps.OrchardCore.Dispatch.Reports.FieldServiceKpiService>();
                services.AddScoped<CrestApps.OrchardCore.Dispatch.Controllers.DispatchTimeZone>();
                services.TryAddScoped<ServiceTerritoryStore>();
                services.TryAddScoped<ServiceTerritoryManager>();
                services.TryAddEnumerable(ServiceDescriptor.Scoped<CrestApps.Core.Services.ICatalogEntryHandler<ServiceTerritory>, ServiceTerritoryHandler>());
                services.AddSingleton(settings.Object);
                services.AddSingleton(geo.Object);
                services.AddSingleton(clock.Object);
                services.AddSingleton(sender.Object);
                services.AddSingleton(site.Object);
                services.AddSingleton(observer.Object);
                services.AddSingleton(equipment.Object);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddCalendarType("Equipment", type => type
                    .WithOwner(CalendarOwnerKind.Resource)
                    .LimitConcurrent(1)
                    .AddEventType("Reservation", new global::OrchardCore.Localization.LocalizationSource("Reservation")));

                if (realLocks)
                {
                    services.AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance));
                }
            },
            async schema =>
            {
                await WorkOrdersIndexSchema.CreateAsync(schema);
                await DispatchIndexSchema.CreateAsync(schema);
                await FieldIndexSchema.CreateAsync(schema);
                await CrestApps.OrchardCore.Assets.Core.Indexes.AssetsIndexSchema.CreateAsync(schema);
                await GeoIndexSchema.CreateAsync(schema);
            },
            new IIndexProvider[]
            {
                new WorkOrderIndexProvider(), new WorkOrderTypeIndexProvider(), new ChecklistTemplateIndexProvider(), new ServiceLevelPolicyIndexProvider(), new WorkOrderSubStatusIndexProvider(),
                new ServiceVisitIndexProvider(), new FieldCommandReceiptIndexProvider(), new WorkerLocationConsentIndexProvider(), new WorkerLocationPingIndexProvider(),
                new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider(),
                new CrestApps.OrchardCore.Assets.Core.Indexes.AssetTypeIndexProvider(), new CrestApps.OrchardCore.Assets.Core.Indexes.AssetIndexProvider(), new CrestApps.OrchardCore.Assets.Core.Indexes.AssetMeterReadingIndexProvider(),
            },
            [WorkOrdersConstants.CollectionName, DispatchConstants.CollectionName, GeoConstants.CollectionName, CrestApps.OrchardCore.Assets.AssetsConstants.CollectionName]);

        kit.Team = await WorkforceTests.AddTeamAsync(kit.Harness, "Field", TeamKind.Team);
        kit.Team = await PlanningKit.UpdateTeamAsync(kit.Harness, kit.Team, team => team.GetWorkforceSettings().Location = new WorkforceLocation { Latitude = Depot.Latitude, Longitude = Depot.Longitude });
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
        var calendarId = await Harness.RunAsync(async services => (await services.GetRequiredService<CrestApps.OrchardCore.Teams.Services.ITeamManager>().FindByIdAsync(Team.ItemId)).GetWorkforceSettings().CalendarId, commit: false);
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

/// <summary>
/// Keeps the technician app's photos and signatures in memory.
/// </summary>
internal sealed class MemoryFieldFileStore : IFieldFileStore
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public async Task<string> SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();

        await content.CopyToAsync(copy, cancellationToken);
        Files[path] = copy.ToArray();

        return path;
    }
}
