using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a waitlisted time-off request gets a place.
/// </summary>
public sealed class WorkforceTimeOffWaitlistPromotedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceTimeOffWaitlistPromotedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceTimeOffWaitlistPromotedEvent(IStringLocalizer<WorkforceTimeOffWaitlistPromotedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.TimeOffWaitlistPromoted;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Time Off Waitlist Promoted"];
}
