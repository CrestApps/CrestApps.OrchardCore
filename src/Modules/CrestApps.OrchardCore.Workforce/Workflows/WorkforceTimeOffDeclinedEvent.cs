using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when time off is declined.
/// </summary>
public sealed class WorkforceTimeOffDeclinedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimeOffDeclinedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimeOffDeclinedEvent(IStringLocalizer<WorkforceTimeOffDeclinedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimeOffDeclined;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Time Off Declined"];
}
