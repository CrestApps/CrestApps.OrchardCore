using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when time off is canceled or withdrawn.
/// </summary>
public sealed class WorkforceTimeOffCanceledEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimeOffCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimeOffCanceledEvent(IStringLocalizer<WorkforceTimeOffCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimeOffCanceled;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Time Off Canceled"];
}
