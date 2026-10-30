using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when someone is invited to a calendar event.
/// </summary>
public sealed class CalendarAttendeeInvitedEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarAttendeeInvitedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarAttendeeInvitedEvent(IStringLocalizer<CalendarAttendeeInvitedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarAttendeeInvitedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Attendee Invited"];
}
