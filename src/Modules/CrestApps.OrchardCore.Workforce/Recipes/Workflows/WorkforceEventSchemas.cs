using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Workforce.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Recipes.Workflows;

/// <summary>
/// The base of the Workforce workflow event schemas: every Workforce event can be limited to some teams and has one
/// outcome.
/// </summary>
internal abstract class WorkforceEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Workforce";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("TeamIds", WorkflowActivitySchemaBuilders.StringArray("The ids of the teams the event starts for, matched against the TeamId of its input; empty for every team."));
    }
}

/// <summary>
/// Describes the Time Off Requested workflow event.
/// </summary>
internal sealed class WorkforceTimeOffRequestedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimeOffRequested;

    protected override string DisplayText => "Time Off Requested";

    protected override string Description => "Starts when time off is asked for. Input: WorkerId, TeamId, RequestId, TypeId, StartDate, EndDate, Status";
}

/// <summary>
/// Describes the Time Off Approved workflow event.
/// </summary>
internal sealed class WorkforceTimeOffApprovedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimeOffApproved;

    protected override string DisplayText => "Time Off Approved";

    protected override string Description => "Starts when time off is approved. Input: WorkerId, TeamId, RequestId, TypeId, StartDate, EndDate";
}

/// <summary>
/// Describes the Time Off Declined workflow event.
/// </summary>
internal sealed class WorkforceTimeOffDeclinedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimeOffDeclined;

    protected override string DisplayText => "Time Off Declined";

    protected override string Description => "Starts when time off is declined. Input: WorkerId, TeamId, RequestId, TypeId, StartDate, EndDate";
}

/// <summary>
/// Describes the Time Off Canceled workflow event.
/// </summary>
internal sealed class WorkforceTimeOffCanceledEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimeOffCanceled;

    protected override string DisplayText => "Time Off Canceled";

    protected override string Description => "Starts when time off is canceled or withdrawn. Input: WorkerId, TeamId, RequestId, TypeId, StartDate, EndDate, Status";
}

/// <summary>
/// Describes the Time Off Waitlist Promoted workflow event.
/// </summary>
internal sealed class WorkforceTimeOffWaitlistPromotedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimeOffWaitlistPromoted;

    protected override string DisplayText => "Time Off Waitlist Promoted";

    protected override string Description => "Starts when a waitlisted time-off request gets a place because another was canceled or withdrawn. Input: WorkerId, TeamId, RequestId, TypeId, StartDate, EndDate, Status";
}

/// <summary>
/// Describes the Schedule Published workflow event.
/// </summary>
internal sealed class WorkforceSchedulePublishedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.SchedulePublished;

    protected override string DisplayText => "Schedule Published";

    protected override string Description => "Starts when a schedule is published. Input: PeriodId, TeamId, StartDate, EndDate, Version";
}

/// <summary>
/// Describes the Punched In workflow event.
/// </summary>
internal sealed class WorkforcePunchedInEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.PunchedIn;

    protected override string DisplayText => "Punched In";

    protected override string Description => "Starts when a worker clocks in. Input: WorkerId, TeamId, PunchId, Utc, Source";
}

/// <summary>
/// Describes the Punched Out workflow event.
/// </summary>
internal sealed class WorkforcePunchedOutEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.PunchedOut;

    protected override string DisplayText => "Punched Out";

    protected override string Description => "Starts when a worker clocks out. Input: WorkerId, TeamId, PunchId, Utc, Source";
}

/// <summary>
/// Describes the Attendance Exception Raised workflow event.
/// </summary>
internal sealed class WorkforceAttendanceExceptionRaisedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.AttendanceExceptionRaised;

    protected override string DisplayText => "Attendance Exception Raised";

    protected override string Description => "Starts when an attendance exception, such as a late punch or a no-show, is raised. Input: WorkerId, TeamId, ExceptionId, Kind, Date, Minutes, Points";
}

/// <summary>
/// Describes the Attendance Points Threshold Reached workflow event.
/// </summary>
internal sealed class WorkforceAttendancePointsThresholdReachedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.AttendancePointsThresholdReached;

    protected override string DisplayText => "Attendance Points Threshold Reached";

    protected override string Description => "Starts when a worker's attendance points reach their time clock policy's threshold. Input: WorkerId, TeamId, ExceptionId, Points, Threshold";
}

/// <summary>
/// Describes the Timesheet Submitted workflow event.
/// </summary>
internal sealed class WorkforceTimesheetSubmittedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimesheetSubmitted;

    protected override string DisplayText => "Timesheet Submitted";

    protected override string Description => "Starts when a timesheet is submitted. Input: WorkerId, TeamId, TimesheetId, PeriodStart, PeriodEnd, Hours";
}

/// <summary>
/// Describes the Timesheet Approved workflow event.
/// </summary>
internal sealed class WorkforceTimesheetApprovedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.TimesheetApproved;

    protected override string DisplayText => "Timesheet Approved";

    protected override string Description => "Starts when a timesheet is approved. Input: WorkerId, TeamId, TimesheetId, PeriodStart, PeriodEnd, Hours";
}

/// <summary>
/// Describes the Shift Assigned workflow event.
/// </summary>
internal sealed class WorkforceShiftAssignedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.ShiftAssigned;

    protected override string DisplayText => "Shift Assigned";

    protected override string Description => "Starts when a shift is added to a published schedule for a worker, such as by a republish, a claimed open shift or canceled time off. Input: WorkerId, TeamId, ShiftKey, Kind, StartUtc, EndUtc, Reason, InitiatedBy";
}

/// <summary>
/// Describes the Shift Changed workflow event.
/// </summary>
internal sealed class WorkforceShiftChangedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.ShiftChanged;

    protected override string DisplayText => "Shift Changed";

    protected override string Description => "Starts when a published shift is moved, given to someone else, swapped or has its breaks changed. Input: WorkerId, TeamId, ShiftKey, Kind, PreviousWorkerId, BeforeStartUtc, BeforeEndUtc, StartUtc, EndUtc, Reason, InitiatedBy";
}

/// <summary>
/// Describes the Shift Removed workflow event.
/// </summary>
internal sealed class WorkforceShiftRemovedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.ShiftRemoved;

    protected override string DisplayText => "Shift Removed";

    protected override string Description => "Starts when a published shift is taken off the schedule. Input: WorkerId, TeamId, ShiftKey, BeforeStartUtc, BeforeEndUtc, Reason, InitiatedBy";
}

/// <summary>
/// Describes the Open Shift Posted workflow event.
/// </summary>
internal sealed class WorkforceOpenShiftPostedEventSchema : WorkforceEventSchemaBase
{
    public override string Name => WorkforceEvents.OpenShiftPosted;

    protected override string DisplayText => "Open Shift Posted";

    protected override string Description => "Starts when a shift with nobody is offered to the team, on its own or by a publish; Shift Assigned follows when it is filled. Input: TeamId, OpenShiftId, Date, StartUtc, EndUtc, ClaimPolicy, ClaimDeadlineUtc";
}
