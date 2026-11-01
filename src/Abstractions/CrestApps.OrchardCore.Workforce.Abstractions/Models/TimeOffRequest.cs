using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's request for time off, and the decision on it.
/// </summary>
public sealed class TimeOffRequest : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's team the request belongs to, which sets who approves it and its calendar.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the policy the time off is taken from; empty for a type that needs no balance.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets when the time off starts, for part of a day.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Gets or sets when the time off ends, for part of a day.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the time zone the days and times are in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets what each day counts.
    /// </summary>
    public IList<TimeOffRequestDay> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets what the request counts in all, in the type's unit.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets where the request stands.
    /// </summary>
    public TimeOffRequestStatus Status { get; set; } = TimeOffRequestStatus.Pending;

    /// <summary>
    /// Gets or sets the worker's reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the note that came with the decision.
    /// </summary>
    public string DecisionNote { get; set; }

    /// <summary>
    /// Gets or sets who decided.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the decision.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether an allotment approved the request with no manager.
    /// </summary>
    public bool AutoApproved { get; set; }

    /// <summary>
    /// Gets or sets who submitted the request.
    /// </summary>
    public string SubmittedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the request was submitted, which orders the waitlist.
    /// </summary>
    public DateTime SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets the Time off event on the team calendar, once approved.
    /// </summary>
    public string CalendarEventId { get; set; }

    /// <summary>
    /// Gets or sets the published shifts the time off overlaps, which were left for the scheduler to cover.
    /// </summary>
    public IList<string> FlaggedShiftEventIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the request was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the request was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether the request still waits for a place or a decision.
    /// </summary>
    public bool IsOpen => Status is TimeOffRequestStatus.Pending or TimeOffRequestStatus.Waitlisted;

    /// <summary>
    /// Gets whether the request covers only part of a day.
    /// </summary>
    public bool IsPartialDay => StartTime.HasValue && EndTime.HasValue;
}
