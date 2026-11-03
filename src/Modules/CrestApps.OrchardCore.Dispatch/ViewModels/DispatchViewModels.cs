using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Dispatch.ViewModels;

/// <summary>
/// The dispatch settings form.
/// </summary>
public class DispatchSettingsViewModel
{
    /// <summary>
    /// Gets or sets whether a new work order gets an unscheduled visit.
    /// </summary>
    public bool CreateVisitsForNewWorkOrders { get; set; }

    /// <summary>
    /// Gets or sets the arrival window, in minutes.
    /// </summary>
    public int ArrivalWindowMinutes { get; set; }

    /// <summary>
    /// Gets or sets how many days ahead candidates are looked for.
    /// </summary>
    public int HorizonDays { get; set; }

    /// <summary>
    /// Gets or sets the buffer between visits, in minutes.
    /// </summary>
    public int BufferMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether a worker must belong to the territory.
    /// </summary>
    public bool StrictTerritory { get; set; }

    /// <summary>
    /// Gets or sets the board's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the travel weight.
    /// </summary>
    public int TravelWeight { get; set; }

    /// <summary>
    /// Gets or sets the urgency weight.
    /// </summary>
    public int UrgencyWeight { get; set; }

    /// <summary>
    /// Gets or sets the proficiency weight.
    /// </summary>
    public int ProficiencyWeight { get; set; }

    /// <summary>
    /// Gets or sets the preferred worker weight.
    /// </summary>
    public int PreferredWeight { get; set; }

    /// <summary>
    /// Gets or sets the workload balance weight.
    /// </summary>
    public int BalanceWeight { get; set; }

    /// <summary>
    /// Gets or sets how many minutes late a visit is at risk.
    /// </summary>
    public int AtRiskMinutes { get; set; }

    /// <summary>
    /// Gets or sets how long the optimizer looks, in seconds.
    /// </summary>
    public int OptimizerSeconds { get; set; }

    /// <summary>
    /// Gets or sets whether customers are told about their visit.
    /// </summary>
    public bool NoticesEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether notices carry a tracking link.
    /// </summary>
    public bool TrackingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the local hour reminders go out the day before.
    /// </summary>
    public int ReminderHour { get; set; }

    /// <summary>
    /// Gets or sets the scheduled notice.
    /// </summary>
    public string ScheduledText { get; set; }

    /// <summary>
    /// Gets or sets the reminder.
    /// </summary>
    public string ReminderText { get; set; }

    /// <summary>
    /// Gets or sets the on-the-way notice.
    /// </summary>
    public string EnRouteText { get; set; }

    /// <summary>
    /// Gets or sets the done notice.
    /// </summary>
    public string CompletedText { get; set; }

    /// <summary>
    /// Gets or sets the canceled notice.
    /// </summary>
    public string CanceledText { get; set; }

    /// <summary>
    /// Gets or sets whether a way to send notices is on.
    /// </summary>
    [BindNever]
    public bool CanSendNotices { get; set; }
}

/// <summary>
/// The customer's tracking page.
/// </summary>
public class VisitTrackingViewModel
{
    /// <summary>
    /// Gets or sets the business's name.
    /// </summary>
    public string Company { get; set; }

    /// <summary>
    /// Gets or sets whether the link no longer works.
    /// </summary>
    public bool Expired { get; set; }

    /// <summary>
    /// Gets or sets the visit number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets where the visit is, in the customer's words.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the technician is on the way.
    /// </summary>
    public bool EnRoute { get; set; }

    /// <summary>
    /// Gets or sets the technician's first name.
    /// </summary>
    public string Technician { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets the arrival window.
    /// </summary>
    public string Window { get; set; }

    /// <summary>
    /// Gets or sets when the technician is expected.
    /// </summary>
    public string Eta { get; set; }
}

/// <summary>
/// The board's request for a better arrangement of a day.
/// </summary>
public sealed class BoardOptimizeModel
{
    /// <summary>
    /// Gets or sets the day, as yyyy-MM-dd.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets the team, if one is shown.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets whether to fit in unscheduled visits.
    /// </summary>
    public bool IncludeUnscheduled { get; set; }
}

/// <summary>
/// A visit in the visits list.
/// </summary>
public sealed class VisitRow
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public ServiceVisit Visit { get; set; }

    /// <summary>
    /// Gets or sets the names of its workers.
    /// </summary>
    public string Workers { get; set; }

    /// <summary>
    /// Gets or sets its type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets now, in UTC.
    /// </summary>
    public DateTime NowUtc { get; set; }
}

/// <summary>
/// The visits list and its filters.
/// </summary>
public class VisitsListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the status filter: open, all, or a status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the worker filter.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the day filter, as yyyy-MM-dd.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets how many open work orders have no visit.
    /// </summary>
    [BindNever]
    public int WithoutVisits { get; set; }

    /// <summary>
    /// Gets or sets the workers to filter by.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WorkerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext<VisitRow> List { get; set; }
}

/// <summary>
/// A visit's page.
/// </summary>
public class VisitDetailsViewModel
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public ServiceVisit Visit { get; set; }

    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; set; }

    /// <summary>
    /// Gets or sets the type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the names of the workers, by id.
    /// </summary>
    public IDictionary<string, string> WorkerNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the time zone times are shown in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the day candidates are found for, as yyyy-MM-dd.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets the candidates, when found.
    /// </summary>
    public DispatchCandidateResult Candidates { get; set; }

    /// <summary>
    /// Gets or sets the workers who can be chosen by hand.
    /// </summary>
    public IList<SelectListItem> WorkerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses the visit can move to.
    /// </summary>
    public IList<VisitStatus> NextStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the other visits of the work order.
    /// </summary>
    public IList<ServiceVisit> OtherVisits { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user can change the visit.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the map settings, when the address is on the map.
    /// </summary>
    public GeoSettings Map { get; set; }

    /// <summary>
    /// Gets or sets the names of the equipment booked, by id.
    /// </summary>
    public IDictionary<string, string> EquipmentNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the customer's tracking link, while it works.
    /// </summary>
    public string TrackingUrl { get; set; }
}

/// <summary>
/// The dispatch board page.
/// </summary>
public class DispatchBoardViewModel
{
    /// <summary>
    /// Gets or sets the day shown, as yyyy-MM-dd.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets the board's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the team filter.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the teams to filter by.
    /// </summary>
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user can move visits.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the address of the live updates hub, when SignalR is on.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the map settings.
    /// </summary>
    public GeoSettings Map { get; set; }
}

/// <summary>
/// A work order's visits, on the work order's page.
/// </summary>
public class WorkOrderVisitsViewModel
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; set; }

    /// <summary>
    /// Gets or sets its visits, oldest first.
    /// </summary>
    public IList<ServiceVisit> Visits { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the workers, by id.
    /// </summary>
    public IDictionary<string, string> WorkerNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the time zone times are shown in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the user can change the work order.
    /// </summary>
    public bool CanManage { get; set; }
}

/// <summary>
/// A move made on the board: a visit dropped on a worker at a time.
/// </summary>
public sealed class BoardMoveModel
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the start, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }
}

/// <summary>
/// The Field page of My Work.
/// </summary>
public class FieldViewModel
{
    /// <summary>
    /// Gets or sets the portal's navigation.
    /// </summary>
    public Workforce.ViewModels.MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the worker, so the device keeps each worker's visits apart.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets how many days after today are kept offline.
    /// </summary>
    public int OfflineDays { get; set; }

    /// <summary>
    /// Gets or sets how long positions are kept, in days.
    /// </summary>
    public int RetentionDays { get; set; }
}
