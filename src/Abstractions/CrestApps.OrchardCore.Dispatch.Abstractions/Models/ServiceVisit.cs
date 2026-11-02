using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.Dispatch.Models;

/// <summary>
/// Where a visit is in its day.
/// </summary>
public enum VisitStatus
{
    /// <summary>
    /// Not yet given a time and a worker.
    /// </summary>
    Unscheduled,

    /// <summary>
    /// Given a time and a worker.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Sent to the worker.
    /// </summary>
    Dispatched,

    /// <summary>
    /// The worker is on the way.
    /// </summary>
    EnRoute,

    /// <summary>
    /// The worker is there.
    /// </summary>
    OnSite,

    /// <summary>
    /// Stopped for a while, such as to fetch a part.
    /// </summary>
    Paused,

    /// <summary>
    /// Done.
    /// </summary>
    Completed,

    /// <summary>
    /// Ended without finishing; another visit is made for what is left.
    /// </summary>
    Incomplete,

    /// <summary>
    /// Not going ahead.
    /// </summary>
    Canceled,
}

/// <summary>
/// A change of a visit's status, kept in its history.
/// </summary>
public sealed class VisitStatusChange
{
    /// <summary>
    /// Gets or sets the status it moved to.
    /// </summary>
    public VisitStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it moved, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets who moved it.
    /// </summary>
    public string By { get; set; }

    /// <summary>
    /// Gets or sets what was said about it.
    /// </summary>
    public string Note { get; set; }
}

/// <summary>
/// The calendar event a visit keeps for one of its resources.
/// </summary>
public sealed class VisitCalendarEvent
{
    /// <summary>
    /// Gets or sets the kind of resource, such as Worker.
    /// </summary>
    public string ResourceKind { get; set; }

    /// <summary>
    /// Gets or sets the resource.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the calendar event.
    /// </summary>
    public string EventId { get; set; }
}

/// <summary>
/// A notice sent to the customer about their visit.
/// </summary>
public sealed class VisitNotice
{
    /// <summary>
    /// Gets or sets the kind of notice.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets when it was sent, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets the channel it went on, such as Sms.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the start it was about, so a moved visit is told again.
    /// </summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>
    /// Gets or sets what went wrong, if it wasn't sent.
    /// </summary>
    public string Error { get; set; }
}

/// <summary>
/// One trip to do a work order's job: when, by whom, and how it went.
/// </summary>
public sealed class ServiceVisit : CatalogItem
{
    /// <summary>
    /// Gets or sets the visit's number, such as V-000001.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the work order the visit is for.
    /// </summary>
    public string WorkOrderId { get; set; }

    /// <summary>
    /// Gets or sets the work order's number, kept for lists.
    /// </summary>
    public string WorkOrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the work order's summary, kept for lists.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the work order's type, kept for lists.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the work order's priority, kept for lists.
    /// </summary>
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    /// <summary>
    /// Gets or sets the work order's territory, kept for lists.
    /// </summary>
    public string TerritoryId { get; set; }

    /// <summary>
    /// Gets or sets when the work order must be resolved by, kept for lists.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public VisitStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how long the job takes, in minutes.
    /// </summary>
    public int DurationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets when the visit starts, in UTC, once scheduled.
    /// </summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the visit ends, in UTC, once scheduled.
    /// </summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the start of the arrival window promised to the customer, in UTC.
    /// </summary>
    public DateTime? ArrivalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the arrival window promised to the customer, in UTC.
    /// </summary>
    public DateTime? ArrivalEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the worker who leads the visit.
    /// </summary>
    public string LeadWorkerId { get; set; }

    /// <summary>
    /// Gets or sets every worker on the visit, the lead first.
    /// </summary>
    public List<string> WorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many people the job takes, from its type.
    /// </summary>
    public int CrewSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the asset types of equipment it needs, from its type.
    /// </summary>
    public List<string> RequiredEquipmentTypeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the equipment booked for it.
    /// </summary>
    public List<string> EquipmentAssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the technician is expected, in UTC, once on the way.
    /// </summary>
    public DateTime? EtaUtc { get; set; }

    /// <summary>
    /// Gets or sets why the visit is at risk, if it is.
    /// </summary>
    public string AtRiskReason { get; set; }

    /// <summary>
    /// Gets or sets since when the visit is at risk, in UTC.
    /// </summary>
    public DateTime? AtRiskSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the notices sent to the customer.
    /// </summary>
    public List<VisitNotice> Notices { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the visit stays where it is when visits are rearranged.
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>
    /// Gets or sets the travel to the visit from the stop before, in minutes.
    /// </summary>
    public int? TravelMinutes { get; set; }

    /// <summary>
    /// Gets or sets the travel to the visit from the stop before, in kilometers.
    /// </summary>
    public double? TravelKm { get; set; }

    /// <summary>
    /// Gets or sets why the visit ended without finishing.
    /// </summary>
    public string IncompleteReason { get; set; }

    /// <summary>
    /// Gets or sets the visit made for what was left when this one ended incomplete.
    /// </summary>
    public string ReturnVisitId { get; set; }

    /// <summary>
    /// Gets or sets the calendar events kept for the visit's resources.
    /// </summary>
    public List<VisitCalendarEvent> CalendarEvents { get; set; } = [];

    /// <summary>
    /// Gets or sets every status change, oldest first.
    /// </summary>
    public List<VisitStatusChange> History { get; set; } = [];

    /// <summary>
    /// Gets or sets when the visit was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the visit last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
