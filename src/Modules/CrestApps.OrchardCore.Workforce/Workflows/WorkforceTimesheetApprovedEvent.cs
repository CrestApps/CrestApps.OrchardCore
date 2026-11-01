using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a timesheet is approved.
/// </summary>
public sealed class WorkforceTimesheetApprovedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimesheetApprovedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimesheetApprovedEvent(IStringLocalizer<WorkforceTimesheetApprovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimesheetApproved;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Timesheet Approved"];
}
