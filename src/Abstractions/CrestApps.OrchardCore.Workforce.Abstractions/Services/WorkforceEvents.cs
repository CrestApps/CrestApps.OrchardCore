namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// The names of the events Workforce raises through <see cref="IWorkforceEvents"/>, which are also the names of their
/// workflow events. Every event's input has <c>WorkerId</c> and <c>TeamId</c> when it is about a worker or a team.
/// </summary>
public static class WorkforceEvents
{
    /// <summary>
    /// Time off was asked for. Input: RequestId, TypeId, StartDate, EndDate, Status.
    /// </summary>
    public const string TimeOffRequested = "WorkforceTimeOffRequestedEvent";

    /// <summary>
    /// Time off was approved. Input: RequestId, TypeId, StartDate, EndDate.
    /// </summary>
    public const string TimeOffApproved = "WorkforceTimeOffApprovedEvent";

    /// <summary>
    /// Time off was declined. Input: RequestId, TypeId, StartDate, EndDate.
    /// </summary>
    public const string TimeOffDeclined = "WorkforceTimeOffDeclinedEvent";

    /// <summary>
    /// Time off was canceled or withdrawn. Input: RequestId, TypeId, StartDate, EndDate, Status.
    /// </summary>
    public const string TimeOffCanceled = "WorkforceTimeOffCanceledEvent";

    /// <summary>
    /// A schedule was published. Input: PeriodId, TeamId, StartDate, EndDate, Version.
    /// </summary>
    public const string SchedulePublished = "WorkforceSchedulePublishedEvent";

    /// <summary>
    /// A worker clocked in. Input: PunchId, Utc, Source.
    /// </summary>
    public const string PunchedIn = "WorkforcePunchedInEvent";

    /// <summary>
    /// A worker clocked out. Input: PunchId, Utc, Source.
    /// </summary>
    public const string PunchedOut = "WorkforcePunchedOutEvent";

    /// <summary>
    /// An attendance exception was raised. Input: ExceptionId, Kind, Date, Minutes, Points.
    /// </summary>
    public const string AttendanceExceptionRaised = "WorkforceAttendanceExceptionRaisedEvent";

    /// <summary>
    /// A worker's attendance points reached their policy's threshold. Input: ExceptionId, Points, Threshold.
    /// </summary>
    public const string AttendancePointsThresholdReached = "WorkforceAttendancePointsThresholdReachedEvent";

    /// <summary>
    /// A timesheet was submitted. Input: TimesheetId, PeriodStart, PeriodEnd, Hours.
    /// </summary>
    public const string TimesheetSubmitted = "WorkforceTimesheetSubmittedEvent";

    /// <summary>
    /// A timesheet was approved. Input: TimesheetId, PeriodStart, PeriodEnd, Hours.
    /// </summary>
    public const string TimesheetApproved = "WorkforceTimesheetApprovedEvent";

    /// <summary>
    /// An agent has been out of adherence with their schedule for longer than their rule allows.
    /// </summary>
    public const string OutOfAdherence = "WorkforceOutOfAdherenceEvent";
}
