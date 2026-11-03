using CrestApps.OrchardCore.Calendars.Core.Models;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Turns an event's own reminders (<see cref="CalendarEvent.ReminderMinutes"/>) into a reminder for each of its people: the
/// owner of a personal calendar and the users invited who haven't declined.
/// </summary>
public sealed class CalendarUserReminderObserver : ICalendarReminderObserver
{
    private readonly CalendarUserReminderStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarUserReminderObserver"/> class.
    /// </summary>
    /// <param name="store">The reminder store.</param>
    /// <param name="clock">The clock.</param>
    public CalendarUserReminderObserver(CalendarUserReminderStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task OnStartingSoonAsync(CalendarReminderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var occurrence = context.Occurrence;
        var calendarEvent = occurrence.Event;
        var minutes = (int)Math.Round(context.Offset.TotalMinutes);

        // The service also raises the offsets workflows asked for; only the event's own reminders reach its people.
        if (!calendarEvent.ReminderMinutes.Contains(minutes) || calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var userId in GetPeople(calendarEvent, context.Calendar))
        {
            await _store.CreateAsync(new CalendarUserReminder
            {
                UserId = userId,
                EventId = calendarEvent.SeriesId ?? calendarEvent.ItemId,
                CalendarId = calendarEvent.CalendarId,
                CalendarName = context.Calendar?.Name,
                Title = calendarEvent.Title,
                Location = calendarEvent.Location,
                StartUtc = occurrence.StartUtc,
                EndUtc = occurrence.EndUtc,
                AllDay = calendarEvent.AllDay,
                MinutesBefore = minutes,
                CreatedUtc = now,
            }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task OnCompletedAsync(CalendarReminderContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the users an event reminds.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="calendar">Its calendar.</param>
    /// <returns>The users' ids.</returns>
    public static IReadOnlyList<string> GetPeople(CalendarEvent calendarEvent, Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var people = new List<string>();

        if (calendar is { OwnerKind: CalendarOwnerKind.User } && !string.IsNullOrEmpty(calendar.OwnerId))
        {
            people.Add(calendar.OwnerId);
        }

        foreach (var attendee in calendarEvent.Attendees)
        {
            if (!string.IsNullOrEmpty(attendee.UserId) && attendee.Response != CalendarAttendeeResponse.Declined && !people.Contains(attendee.UserId, StringComparer.Ordinal))
            {
                people.Add(attendee.UserId);
            }
        }

        return people;
    }
}
