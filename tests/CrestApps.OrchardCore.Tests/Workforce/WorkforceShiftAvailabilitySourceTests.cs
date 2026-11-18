using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Who is off their Workforce schedule now: a worker on a shift covering now can take work, unless now falls in a break
/// inside it; a worker whose shifts are later today or tomorrow can't; a user with no shift around now isn't judged.
/// </summary>
public sealed class WorkforceShiftAvailabilitySourceTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OnlyWorkersOffTheirShiftOrOnABreak_AreUnavailable()
    {
        // Arrange
        var queries = new Mock<ICalendarQueryService>();
        queries.Setup(service => service.GetOccurrencesAsync(It.IsAny<CalendarOccurrenceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Shift("on-shift", _now.AddHours(-2), _now.AddHours(6)),
                Shift("on-break", _now.AddHours(-2), _now.AddHours(6), new ShiftSegment { Kind = ActivityKind.Break, StartUtc = _now.AddMinutes(-10), EndUtc = _now.AddMinutes(20) }),
                Shift("later", _now.AddHours(4), _now.AddHours(12)),
                Shift("cancelled-now", _now.AddHours(-1), _now.AddHours(3), status: CalendarEventStatus.Cancelled),
                Shift("cancelled-now", _now.AddDays(1), _now.AddDays(1).AddHours(8)),
            ]);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var source = new WorkforceShiftAvailabilitySource(queries.Object, clock.Object);

        // Act
        var unavailable = await source.GetUnavailableAsync(["on-shift", "on-break", "later", "cancelled-now", "no-shifts"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["cancelled-now", "later", "on-break"], unavailable.Order(StringComparer.Ordinal));
    }

    private static CalendarOccurrence Shift(string userId, DateTime startUtc, DateTime endUtc, ShiftSegment segment = null, CalendarEventStatus status = CalendarEventStatus.Confirmed)
    {
        var calendarEvent = new CalendarEvent
        {
            ItemId = Guid.NewGuid().ToString("N"),
            EventType = WorkforceConstants.EventTypes.Shift,
            Status = status,
            Attendees = [new CalendarAttendee { UserId = userId }],
        };

        calendarEvent.Put(new ShiftData { WorkerId = userId, Segments = segment is null ? [] : [segment] });

        return new CalendarOccurrence { StartUtc = startUtc, EndUtc = endUtc, Event = calendarEvent };
    }
}
