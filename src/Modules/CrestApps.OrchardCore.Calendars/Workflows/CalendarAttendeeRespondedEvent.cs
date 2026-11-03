using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when an attendee answers an invitation.
/// </summary>
public sealed class CalendarAttendeeRespondedEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarAttendeeRespondedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarAttendeeRespondedEvent(IStringLocalizer<CalendarAttendeeRespondedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarAttendeeRespondedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Attendee Response Received"];
}
