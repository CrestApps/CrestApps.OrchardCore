using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a shift is added to a published schedule for a worker.
/// </summary>
public sealed class WorkforceShiftAssignedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceShiftAssignedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceShiftAssignedEvent(IStringLocalizer<WorkforceShiftAssignedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.ShiftAssigned;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Shift Assigned"];
}
