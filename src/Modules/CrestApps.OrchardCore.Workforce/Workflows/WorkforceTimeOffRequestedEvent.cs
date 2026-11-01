using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a worker asks for time off.
/// </summary>
public sealed class WorkforceTimeOffRequestedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimeOffRequestedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimeOffRequestedEvent(IStringLocalizer<WorkforceTimeOffRequestedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimeOffRequested;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Time Off Requested"];
}
