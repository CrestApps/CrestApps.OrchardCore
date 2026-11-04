using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when time off is approved, by a manager, an allotment or the waitlist.
/// </summary>
public sealed class WorkforceTimeOffApprovedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimeOffApprovedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimeOffApprovedEvent(IStringLocalizer<WorkforceTimeOffApprovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimeOffApproved;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Time Off Approved"];
}
