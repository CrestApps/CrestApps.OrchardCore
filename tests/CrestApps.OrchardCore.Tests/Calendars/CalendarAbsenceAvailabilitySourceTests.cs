using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Who is away now because of a calendar: an out-of-office event on one's own calendar, or one on another calendar one
/// attends and hasn't declined; a meeting doesn't make anyone away, and neither does a cancelled absence.
/// </summary>
public sealed class CalendarAbsenceAvailabilitySourceTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AnAbsenceOnOnesCalendarOrAttended_MakesOneAway_AndAMeetingOrACancelledOneDoesnt()
    {
        // Arrange
        var personal = new Calendar { ItemId = "cal-ana", Type = CalendarsConstants.Types.Personal, OwnerKind = CalendarOwnerKind.User, OwnerId = "ana" };
        var team = new Calendar { ItemId = "cal-team", Type = "Team", OwnerKind = CalendarOwnerKind.Role, OwnerId = "Support" };

        var calendars = new Mock<ICalendarService>();
        calendars.Setup(service => service.ListAsync(It.IsAny<CalendarListQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([personal]);
        calendars.Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([team]);

        var queries = new Mock<ICalendarQueryService>();
        queries.Setup(service => service.GetOccurrencesAsync(It.Is<CalendarOccurrenceQuery>(query => query.CalendarIds.Count > 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Occurrence(personal.ItemId, CalendarsConstants.EventTypes.OutOfOffice)]);
        queries.Setup(service => service.GetOccurrencesAsync(It.Is<CalendarOccurrenceQuery>(query => query.AttendeeUserIds.Count > 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Occurrence(team.ItemId, CalendarsConstants.EventTypes.OutOfOffice, ("ben", CalendarAttendeeResponse.Accepted), ("cy", CalendarAttendeeResponse.Declined)),
                Occurrence(team.ItemId, "Meeting", ("dan", CalendarAttendeeResponse.Accepted)),
                Occurrence(team.ItemId, CalendarsConstants.EventTypes.OutOfOffice, CalendarEventStatus.Cancelled, ("eve", CalendarAttendeeResponse.Accepted)),
            ]);

        var types = new Mock<ICalendarTypeManager>();
        types.Setup(manager => manager.FindEventType(It.IsAny<string>(), CalendarsConstants.EventTypes.OutOfOffice))
            .Returns(new CalendarEventTypeDescriptor(CalendarsConstants.EventTypes.OutOfOffice, LocalizationSource.Create<CalendarAbsenceAvailabilitySourceTests>("Out of office")) { MarksAbsence = true });
        types.Setup(manager => manager.FindEventType(It.IsAny<string>(), "Meeting"))
            .Returns(new CalendarEventTypeDescriptor("Meeting", LocalizationSource.Create<CalendarAbsenceAvailabilitySourceTests>("Meeting")));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var source = new CalendarAbsenceAvailabilitySource(calendars.Object, queries.Object, types.Object, clock.Object);

        // Act
        var away = await source.GetUnavailableAsync(["ana", "ben", "cy", "dan", "eve"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ana", "ben"], away.Order(StringComparer.Ordinal));
    }

    private static CalendarOccurrence Occurrence(string calendarId, string eventType, params (string UserId, CalendarAttendeeResponse Response)[] attendees)
        => Occurrence(calendarId, eventType, CalendarEventStatus.Confirmed, attendees);

    private static CalendarOccurrence Occurrence(string calendarId, string eventType, CalendarEventStatus status, params (string UserId, CalendarAttendeeResponse Response)[] attendees)
        => new()
        {
            StartUtc = _now.AddHours(-2),
            EndUtc = _now.AddHours(6),
            Event = new CalendarEvent
            {
                ItemId = Guid.NewGuid().ToString("N"),
                CalendarId = calendarId,
                EventType = eventType,
                Status = status,
                Attendees = [.. attendees.Select(attendee => new CalendarAttendee { UserId = attendee.UserId, Response = attendee.Response })],
            },
        };
}
