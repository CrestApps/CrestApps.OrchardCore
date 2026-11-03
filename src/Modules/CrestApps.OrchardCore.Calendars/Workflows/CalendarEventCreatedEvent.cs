using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when a calendar event is created.
/// </summary>
public sealed class CalendarEventCreatedEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventCreatedEvent(IStringLocalizer<CalendarEventCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarEventCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Event Created"];
}
