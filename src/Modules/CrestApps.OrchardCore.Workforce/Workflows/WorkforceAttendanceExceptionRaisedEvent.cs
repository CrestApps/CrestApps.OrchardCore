using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a late start, missed punch, no-show or other attendance exception is raised.
/// </summary>
public sealed class WorkforceAttendanceExceptionRaisedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceAttendanceExceptionRaisedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceAttendanceExceptionRaisedEvent(IStringLocalizer<WorkforceAttendanceExceptionRaisedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.AttendanceExceptionRaised;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Attendance Exception Raised"];
}
