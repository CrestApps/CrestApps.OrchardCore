using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a worker submits their timesheet.
/// </summary>
public sealed class WorkforceTimesheetSubmittedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimesheetSubmittedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimesheetSubmittedEvent(IStringLocalizer<WorkforceTimesheetSubmittedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimesheetSubmitted;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Timesheet Submitted"];
}
