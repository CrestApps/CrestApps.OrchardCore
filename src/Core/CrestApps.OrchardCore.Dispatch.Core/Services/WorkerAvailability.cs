using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// When a worker can work over a period: their shifts without breaks and time off, and the visits they already have.
/// </summary>
public sealed class WorkerAvailability
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public StaffMember Worker { get; set; }

    /// <summary>
    /// Gets or sets the worker's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where the worker starts from.
    /// </summary>
    public GeoPoint? Home { get; set; }

    /// <summary>
    /// Gets or sets the team calendars the worker's job events go on.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the spans they are on shift and free of breaks and time off, earliest first.
    /// </summary>
    public List<TimeWindow> Open { get; set; } = [];

    /// <summary>
    /// Gets or sets whether any shift falls in the period.
    /// </summary>
    public bool HasShift { get; set; }

    /// <summary>
    /// Gets or sets the time off in the period.
    /// </summary>
    public List<TimeWindow> TimeOff { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's other visits that keep them busy, earliest first.
    /// </summary>
    public List<ServiceVisit> Visits { get; set; } = [];
}

/// <summary>
/// Reads what dispatch needs to know about workers, places and travel.
/// </summary>
public sealed class DispatchResources
{
    private readonly IStaffMemberManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IShiftService _shiftService;
    private readonly ICalendarQueryService _queryService;
    private readonly ISession _session;
    private readonly WorkOrderStore _workOrderStore;
    private readonly IEnumerable<IGeoService> _geoServices;
    private readonly IDispatchSettingsProvider _settingsProvider;
    private readonly IStaffTimeZoneResolver _staffTimeZones;
    private readonly Dictionary<string, GeoPoint?> _places = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Team> _teams = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchResources"/> class.
    /// </summary>
    public DispatchResources(
        IStaffMemberManager workerManager,
        ITeamManager teamManager,
        IShiftService shiftService,
        ICalendarQueryService queryService,
        ISession session,
        WorkOrderStore workOrderStore,
        IEnumerable<IGeoService> geoServices,
        IDispatchSettingsProvider settingsProvider,
        IStaffTimeZoneResolver staffTimeZones)
    {
        _settingsProvider = settingsProvider;
        _staffTimeZones = staffTimeZones;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _shiftService = shiftService;
        _queryService = queryService;
        _session = session;
        _workOrderStore = workOrderStore;
        _geoServices = geoServices;
    }

    /// <summary>
    /// Gets whether travel can be measured.
    /// </summary>
    public bool MeasuresTravel => _geoServices.Any();

    /// <summary>
    /// Lists the active workers.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workers.</returns>
    public async Task<IReadOnlyList<StaffMember>> ListWorkersAsync(CancellationToken cancellationToken = default)
    {
        var (_, workers) = await _workerManager.SearchAsync(new StaffQuery { Status = StaffStatus.Active }, 0, 1000, cancellationToken);

        return workers;
    }

    /// <summary>
    /// Finds a worker.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The worker, or <see langword="null"/>.</returns>
    public async Task<StaffMember> FindWorkerAsync(string workerId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(workerId) ? null : await _workerManager.FindByIdAsync(workerId, cancellationToken);

    /// <summary>
    /// Reads when a worker can work between two times.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="fromUtc">The start, in UTC.</param>
    /// <param name="toUtc">The end, in UTC.</param>
    /// <param name="ignoreVisitId">A visit to leave out, such as the one being moved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The availability.</returns>
    public async Task<WorkerAvailability> GetAvailabilityAsync(StaffMember worker, DateTime fromUtc, DateTime toUtc, string ignoreVisitId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var date = DateOnly.FromDateTime(fromUtc);
        var memberships = worker.MembershipsOn(date).Select(membership => membership.TeamId).ToList();

        if (!string.IsNullOrEmpty(worker.PrimaryTeamId) && memberships.Remove(worker.PrimaryTeamId))
        {
            memberships.Insert(0, worker.PrimaryTeamId);
        }

        var teams = new List<Team>();

        foreach (var teamId in memberships.Distinct(StringComparer.Ordinal))
        {
            var team = await FindTeamAsync(teamId, cancellationToken);

            if (team is not null)
            {
                teams.Add(team);
            }
        }

        var availability = new WorkerAvailability
        {
            Worker = worker,
            TimeZoneId = await TimeZoneOfAsync(worker, teams, cancellationToken),
            Home = GeoPoint.From(worker.HomeBase?.Latitude, worker.HomeBase?.Longitude)
                ?? teams.Select(team => GeoPoint.From(team.GetWorkforceSettings().Location?.Latitude, team.GetWorkforceSettings().Location?.Longitude)).FirstOrDefault(point => point is not null),
            CalendarId = teams.Select(team => team.GetWorkforceSettings().CalendarId).FirstOrDefault(id => !string.IsNullOrEmpty(id)),
        };

        var shifts = await _shiftService.ListWorkerShiftsAsync(worker.ItemId, fromUtc, toUtc, cancellationToken);
        var blocked = new List<TimeWindow>();

        foreach (var shift in shifts)
        {
            availability.HasShift = true;
            availability.Open.Add(new TimeWindow(shift.StartUtc, shift.EndUtc));

            if (shift.TryGet<ShiftData>(out var data))
            {
                blocked.AddRange(data.Segments.Where(segment => segment.Kind != ActivityKind.Work).Select(segment => new TimeWindow(segment.StartUtc, segment.EndUtc)));
            }
        }

        var calendarIds = teams.Select(team => team.GetWorkforceSettings().CalendarId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (calendarIds.Length > 0)
        {
            var timeOff = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = calendarIds,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                EventTypes = [WorkforceConstants.EventTypes.TimeOff],
                Sources = [WorkforceConstants.Source],
            }, cancellationToken);

            foreach (var occurrence in timeOff)
            {
                if (occurrence.Event.Status != CalendarEventStatus.Cancelled &&
                    occurrence.Event.TryGet<TimeOffEventData>(out var data) &&
                    data.WorkerId == worker.ItemId)
                {
                    availability.TimeOff.Add(new TimeWindow(occurrence.StartUtc, occurrence.EndUtc));
                }
            }
        }

        blocked.AddRange(availability.TimeOff);
        availability.Open = [.. TimeWindow.Subtract(availability.Open, blocked)];
        availability.Visits = (await ListWorkerVisitsAsync(worker.ItemId, fromUtc.AddHours(-12), toUtc.AddHours(12), ignoreVisitId, cancellationToken)).ToList();

        return availability;
    }

    /// <summary>
    /// Gives a worker's time zone: the one Staff gives everywhere (their own, their main team's or a parent team's),
    /// else another team's, else the one dispatch shows.
    /// </summary>
    private async Task<string> TimeZoneOfAsync(StaffMember worker, List<Team> teams, CancellationToken cancellationToken)
    {
        if (await _staffTimeZones.FindTimeZoneIdAsync(worker, cancellationToken) is { } zoneFromStaff && Valid(zoneFromStaff))
        {
            return zoneFromStaff;
        }

        foreach (var team in teams)
        {
            if (Valid(team.TimeZoneId))
            {
                return team.TimeZoneId;
            }

            foreach (var ancestor in await _teamManager.GetAncestorsAsync(team.ItemId, cancellationToken))
            {
                if (Valid(ancestor.TimeZoneId))
                {
                    return ancestor.TimeZoneId;
                }
            }
        }

        var zone = await _settingsProvider.GetTimeZoneIdAsync();

        return Valid(zone) ? zone : "UTC";
    }

    private static bool Valid(string zone) => !string.IsNullOrEmpty(zone) && Calendars.CalendarTime.IsValidZone(zone);

    /// <summary>
    /// Lists a worker's visits that keep them busy and overlap a time.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="fromUtc">The start, in UTC.</param>
    /// <param name="toUtc">The end, in UTC.</param>
    /// <param name="ignoreVisitId">A visit to leave out.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The visits, earliest first.</returns>
    public async Task<IReadOnlyList<ServiceVisit>> ListWorkerVisitsAsync(string workerId, DateTime fromUtc, DateTime toUtc, string ignoreVisitId = null, CancellationToken cancellationToken = default)
    {
        var visits = await _session.Query<ServiceVisit, VisitWorkerIndex>(index => index.WorkerId == workerId && index.IsActive && index.StartUtc < toUtc && index.EndUtc > fromUtc, collection: DispatchConstants.CollectionName)
            .ListAsync(cancellationToken);

        return visits
            .Where(visit => visit.ItemId != ignoreVisitId)
            .DistinctBy(visit => visit.ItemId)
            .OrderBy(visit => visit.StartUtc)
            .ToArray();
    }

    /// <summary>
    /// Finds where a work order's job is.
    /// </summary>
    /// <param name="workOrderId">The work order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The place, or <see langword="null"/> when it isn't known.</returns>
    public async Task<GeoPoint?> PlaceOfAsync(string workOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(workOrderId))
        {
            return null;
        }

        if (!_places.TryGetValue(workOrderId, out var place))
        {
            var workOrder = await _workOrderStore.FindByIdAsync(workOrderId, cancellationToken);

            place = GeoPoint.From(workOrder?.ServiceAddress?.Latitude, workOrder?.ServiceAddress?.Longitude);
            _places[workOrderId] = place;
        }

        return place;
    }

    /// <summary>
    /// Measures the trip between two places.
    /// </summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="departureUtc">When it starts, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The trip, or <see langword="null"/> when a place isn't known or travel can't be measured.</returns>
    public async Task<TravelLeg?> TravelAsync(GeoPoint? from, GeoPoint? to, DateTime departureUtc, CancellationToken cancellationToken = default)
    {
        var geo = _geoServices.FirstOrDefault();

        if (geo is null || from is null || to is null)
        {
            return null;
        }

        if (from.Value == to.Value)
        {
            return new TravelLeg(TimeSpan.Zero, 0);
        }

        var matrix = await geo.GetMatrixAsync([from.Value], [to.Value], departureUtc, null, cancellationToken);

        return matrix?.Legs.Count > 0 && matrix.Legs[0].Count > 0 ? matrix[0, 0] : null;
    }

    /// <summary>
    /// Finds a team.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The team, or <see langword="null"/>.</returns>
    public async Task<Team> FindTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            return null;
        }

        if (!_teams.TryGetValue(teamId, out var team))
        {
            team = await _teamManager.FindByIdAsync(teamId, cancellationToken);
            _teams[teamId] = team;
        }

        return team;
    }
}
