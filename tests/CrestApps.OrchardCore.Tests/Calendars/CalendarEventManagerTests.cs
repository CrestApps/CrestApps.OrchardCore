using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The event manager on a real SQLite store: validation, overlap limits, resources, repeating events and their occurrences.
/// </summary>
public sealed class CalendarEventManagerTests
{
    [Fact]
    public async Task Room_RefusesAnOverlappingEvent_ButTakesOneBackToBack()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Board room");

        await harness.CreateEventAsync(room.ItemId, Local(9), Local(10));

        // Act
        var overlapping = await harness.CreateEventAsync(room.ItemId, Local(9, 30), Local(10, 30));
        var backToBack = await harness.CreateEventAsync(room.ItemId, Local(10), Local(11));

        // Assert
        Assert.False(overlapping.Succeeded);
        Assert.Equal(CalendarError.ConflictCode, Assert.Single(overlapping.Errors).Code);
        Assert.True(backToBack.Succeeded);
    }

    [Fact]
    public async Task Office_TakesEventsUpToItsCapacity_AndIgnoresFreeEventTypes()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var office = await harness.CreateCalendarAsync(CalendarTestHarness.Office, "Third floor");

        // Act
        var first = await harness.CreateEventAsync(office.ItemId, Local(9), Local(17), e => e.EventType = "InOffice");
        var second = await harness.CreateEventAsync(office.ItemId, Local(9), Local(17), e => e.EventType = "InOffice");
        var closed = await harness.CreateEventAsync(office.ItemId, Local(9), Local(17), e =>
        {
            e.EventType = "Closed";
            e.Transparency = CalendarTransparency.Free;
        });
        var third = await harness.CreateEventAsync(office.ItemId, Local(12), Local(13), e => e.EventType = "InOffice");

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.True(closed.Succeeded);
        Assert.False(third.Succeeded);
    }

    [Fact]
    public async Task InvitedRoom_IsBookedAcrossCalendars_UnlessItDeclined()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Huddle room");
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");

        await harness.CreateEventAsync(general.ItemId, Local(9), Local(10), e => e.Attendees.Add(new CalendarAttendee { ResourceCalendarId = room.ItemId }));

        // Act
        var clash = await harness.CreateEventAsync(general.ItemId, Local(9, 30), Local(11), e => e.Attendees.Add(new CalendarAttendee { ResourceCalendarId = room.ItemId }));
        var declined = await harness.CreateEventAsync(general.ItemId, Local(9, 30), Local(11), e => e.Attendees.Add(new CalendarAttendee
        {
            ResourceCalendarId = room.ItemId,
            Response = CalendarAttendeeResponse.Declined,
        }));
        var onRoomItself = await harness.CreateEventAsync(room.ItemId, Local(9, 45), Local(10, 15));

        // Assert
        Assert.False(clash.Succeeded);
        Assert.True(declined.Succeeded);
        Assert.False(onRoomItself.Succeeded);
    }

    [Fact]
    public async Task RepeatingEvent_IsRefused_WhenOneOfItsOccurrencesClashes()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Training room");

        await harness.CreateEventAsync(room.ItemId, Local(9, day: 16), Local(10, day: 16));

        // Act (weekly from Monday 2 November, so the third occurrence is Monday 16 November)
        var weekly = await harness.CreateEventAsync(room.ItemId, Local(9), Local(10), e => e.Recurrence = new CalendarRecurrence { Rule = "FREQ=WEEKLY;COUNT=4" });

        // Assert
        Assert.False(weekly.Succeeded);
        Assert.Contains("Training room", Assert.Single(weekly.Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentBookings_OfTheLastPlace_OnlyOneCommits()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var room = await harness.CreateCalendarAsync(CalendarTestHarness.Room, "Focus room");

        await using var first = harness.CreateScope();
        await using var second = harness.CreateScope();

        // Act (both requests check the room before either commits)
        var firstResult = await NewEventAsync(first.ServiceProvider, room.ItemId);
        var secondResult = await NewEventAsync(second.ServiceProvider, room.ItemId);

        await first.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Record.ExceptionAsync(() => second.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.True(firstResult.Succeeded);
        Assert.True(secondResult.Succeeded);
        Assert.IsType<ConcurrencyException>(exception);

        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [room.ItemId], FromUtc = CalendarTestHarness.Now.AddDays(-1), ToUtc = CalendarTestHarness.Now.AddDays(2) });

        Assert.Single(occurrences);

        static async Task<CalendarResult<CalendarEvent>> NewEventAsync(IServiceProvider services, string calendarId)
        {
            var calendar = await services.GetRequiredService<ICalendarService>().FindByIdAsync(calendarId);
            var events = services.GetRequiredService<ICalendarEventManager>();
            var calendarEvent = await events.NewAsync(calendar);

            calendarEvent.Title = "Race";
            calendarEvent.Start = Local(14);
            calendarEvent.End = Local(15);

            return await events.CreateAsync(calendarEvent);
        }
    }

    [Fact]
    public async Task Validation_RefusesBadTimesUnknownTypesAndMissingAttendees()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var workforce = await harness.CreateCalendarAsync(CalendarTestHarness.Workforce, "Support team", c => c.OwnerId = "Support");

        // Act
        var backwards = await harness.CreateEventAsync(general.ItemId, Local(10), Local(10));
        var unknownType = await harness.CreateEventAsync(general.ItemId, Local(10), Local(11), e => e.EventType = "Shift");
        var shiftWithoutPeople = await harness.CreateEventAsync(workforce.ItemId, Local(8), Local(16), e => e.EventType = "Shift");
        var shift = await harness.CreateEventAsync(workforce.ItemId, Local(8), Local(16), e =>
        {
            e.EventType = "Shift";
            e.Attendees.Add(new CalendarAttendee { UserId = "user1", Name = "John Smith" });
        });
        var badRule = await harness.CreateEventAsync(general.ItemId, Local(10), Local(11), e => e.Recurrence = new CalendarRecurrence { Rule = "FREQ=SOMETIMES" });

        // Assert
        Assert.Contains(backwards.Errors, error => error.Field == nameof(CalendarEvent.End));
        Assert.Contains(unknownType.Errors, error => error.Field == nameof(CalendarEvent.EventType));
        Assert.Contains(shiftWithoutPeople.Errors, error => error.Field == nameof(CalendarEvent.Attendees));
        Assert.True(shift.Succeeded);
        Assert.Contains(badRule.Errors, error => error.Field == "Recurrence");
    }

    [Fact]
    public async Task NewEvent_UsesTheEventTypeDefaults()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var workforce = await harness.CreateCalendarAsync(CalendarTestHarness.Workforce, "Support team", c => c.OwnerId = "Support");

        // Act
        var timeOff = await harness.RunAsync(async services =>
        {
            var calendar = await services.GetRequiredService<ICalendarService>().FindByIdAsync(workforce.ItemId);

            return await services.GetRequiredService<ICalendarEventManager>().NewAsync(calendar, "TimeOff");
        }, commit: false);

        // Assert
        Assert.True(timeOff.AllDay);
        Assert.Equal(CalendarEventStatus.Tentative, timeOff.Status);
        Assert.Equal("America/New_York", timeOff.TimeZoneId);
        Assert.Equal(timeOff.Start.AddDays(1), timeOff.End);
    }

    [Fact]
    public async Task ChangingOneOccurrence_ReplacesItWithAnOverride_AndCancellingAnotherRemovesIt()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var series = (await harness.CreateEventAsync(general.ItemId, Local(9), Local(10), e => e.Recurrence = new CalendarRecurrence { Rule = "FREQ=DAILY;COUNT=5" })).Value;
        var second = series.StartUtc.AddDays(1);
        var third = series.StartUtc.AddDays(2);

        // Act
        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var loaded = await events.FindByIdAsync(series.ItemId);
            var moved = await events.UpdateOccurrenceAsync(loaded, second, CalendarEditScope.ThisOccurrence, occurrence =>
            {
                occurrence.Start = occurrence.Start.AddHours(2);
                occurrence.End = occurrence.End.AddHours(2);
                occurrence.Title = "Moved";
            });

            Assert.True(moved.Succeeded, string.Join("; ", moved.Errors.Select(error => error.Message)));
        });

        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var loaded = await events.FindByIdAsync(series.ItemId);
            var cancelled = await events.CancelOccurrenceAsync(loaded, third, CalendarEditScope.ThisOccurrence);

            Assert.True(cancelled.Succeeded);
        });

        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [general.ItemId], FromUtc = series.StartUtc.AddDays(-1), ToUtc = series.StartUtc.AddDays(10) });

        // Assert
        Assert.Equal(4, occurrences.Count);
        var moved = Assert.Single(occurrences, occurrence => occurrence.IsOverride);
        Assert.Equal("Moved", moved.Event.Title);
        Assert.Equal(second.AddHours(2), moved.StartUtc);
        Assert.Equal(second, moved.OriginalStartUtc);
        Assert.DoesNotContain(occurrences, occurrence => occurrence.StartUtc == third);
    }

    [Fact]
    public async Task ChangingThisAndFollowing_SplitsTheSeries()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var series = (await harness.CreateEventAsync(general.ItemId, Local(9), Local(10), e => e.Recurrence = new CalendarRecurrence { Rule = "FREQ=DAILY;COUNT=6" })).Value;
        var fourth = series.StartUtc.AddDays(3);

        // Act
        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var loaded = await events.FindByIdAsync(series.ItemId);
            var result = await events.UpdateOccurrenceAsync(loaded, fourth, CalendarEditScope.ThisAndFollowing, occurrence =>
            {
                occurrence.Start = occurrence.Start.AddHours(1);
                occurrence.End = occurrence.End.AddHours(1);
            });

            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));
        });

        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [general.ItemId], FromUtc = series.StartUtc.AddDays(-1), ToUtc = series.StartUtc.AddDays(10) });

        // Assert
        Assert.Equal(6, occurrences.Count);
        Assert.Equal(3, occurrences.Count(occurrence => occurrence.StartUtc.Hour == series.StartUtc.Hour));
        Assert.Equal(3, occurrences.Count(occurrence => occurrence.StartUtc.Hour == series.StartUtc.Hour + 1));
        Assert.Equal(2, occurrences.Select(occurrence => occurrence.SeriesId).Distinct().Count());
    }

    [Fact]
    public async Task RefusedUpdate_PutsTheEventBack()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var created = (await harness.CreateEventAsync(general.ItemId, Local(9), Local(10))).Value;

        // Act
        var (result, after) = await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            var loaded = await events.FindByIdAsync(created.ItemId);
            var update = await events.UpdateAsync(loaded, e =>
            {
                e.Title = "Changed";
                e.End = e.Start.AddHours(-1);
                e.Start = e.Start.AddHours(1);
            });

            return (update, loaded);
        }, commit: false);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Test", after.Title);
        Assert.Equal(Local(9), after.Start);
    }

    [Fact]
    public async Task Changes_AreQueuedForTheObservers_WithAttendeeChanges()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");

        // Act
        var pending = await harness.RunAsync(async services =>
        {
            var calendar = await services.GetRequiredService<ICalendarService>().FindByIdAsync(general.ItemId);
            var events = services.GetRequiredService<ICalendarEventManager>();
            var calendarEvent = await events.NewAsync(calendar);

            calendarEvent.Title = "Planning";
            calendarEvent.Attendees.Add(new CalendarAttendee { Email = "jane@contoso.com", Name = "Jane" });
            await events.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = CalendarsConstants.WorkflowActor });
            await events.RespondAsync(calendarEvent, "email:jane@contoso.com", CalendarAttendeeResponse.Accepted);

            return services.GetRequiredService<CalendarChangeQueue>().Pending.ToList();
        });

        // Assert
        Assert.Equal(
            [CalendarEventChangeKind.Created, CalendarEventChangeKind.AttendeeAdded, CalendarEventChangeKind.Updated, CalendarEventChangeKind.ResponseChanged],
            pending.Select(change => change.Kind));
        Assert.Equal(CalendarsConstants.WorkflowActor, pending[0].Actor);
        Assert.Equal(CalendarAttendeeResponse.Accepted, pending[3].Attendee.Response);
    }

    [Fact]
    public async Task CancellingASeries_HidesItAndItsOverrides()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var general = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");
        var series = (await harness.CreateEventAsync(general.ItemId, Local(9), Local(10), e => e.Recurrence = new CalendarRecurrence { Rule = "FREQ=DAILY;COUNT=3" })).Value;

        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            await events.UpdateOccurrenceAsync(await events.FindByIdAsync(series.ItemId), series.StartUtc.AddDays(1), CalendarEditScope.ThisOccurrence, occurrence => occurrence.Title = "Special");
        });

        // Act
        await harness.RunAsync(async services =>
        {
            var events = services.GetRequiredService<ICalendarEventManager>();
            await events.CancelAsync(await events.FindByIdAsync(series.ItemId));
        });

        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [general.ItemId], FromUtc = series.StartUtc.AddDays(-1), ToUtc = series.StartUtc.AddDays(5) });

        // Assert
        Assert.Empty(occurrences);
    }

    private static DateTime Local(int hour, int minute = 0, int day = 2)
    {
        return new DateTime(2026, 11, day, hour, minute, 0);
    }
}
