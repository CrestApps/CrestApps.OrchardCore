using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when an occurrence of a calendar event ends. Needs the Calendar Reminders feature.
/// </summary>
public sealed class CalendarEventCompletedEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventCompletedEvent(IStringLocalizer<CalendarEventCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarEventCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Event Completed"];
}
