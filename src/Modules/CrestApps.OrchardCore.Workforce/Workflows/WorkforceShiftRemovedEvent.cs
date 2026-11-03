using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a published shift is taken off the schedule.
/// </summary>
public sealed class WorkforceShiftRemovedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceShiftRemovedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceShiftRemovedEvent(IStringLocalizer<WorkforceShiftRemovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.ShiftRemoved;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Shift Removed"];
}
