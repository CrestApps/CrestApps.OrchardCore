using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a team schedule is published.
/// </summary>
public sealed class WorkforceSchedulePublishedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceSchedulePublishedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceSchedulePublishedEvent(IStringLocalizer<WorkforceSchedulePublishedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.SchedulePublished;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Schedule Published"];
}
