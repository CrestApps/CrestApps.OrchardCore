using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a worker's attendance points reach their policy's threshold.
/// </summary>
public sealed class WorkforceAttendancePointsThresholdReachedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceAttendancePointsThresholdReachedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceAttendancePointsThresholdReachedEvent(IStringLocalizer<WorkforceAttendancePointsThresholdReachedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.AttendancePointsThresholdReached;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Attendance Points Threshold Reached"];
}
