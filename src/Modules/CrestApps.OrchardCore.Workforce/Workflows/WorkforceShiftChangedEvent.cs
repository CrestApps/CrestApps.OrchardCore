using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a published shift is moved, given to someone else, swapped or has its breaks changed.
/// </summary>
public sealed class WorkforceShiftChangedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceShiftChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceShiftChangedEvent(IStringLocalizer<WorkforceShiftChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.ShiftChanged;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Shift Changed"];
}
