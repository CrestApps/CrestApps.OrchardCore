using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Creates, changes, cancels and finds events. Every write validates the event against its calendar and type, enforces the
/// calendar's overlap limit under a lock, runs the <see cref="ICalendarEventHandler"/>s and, after the change is committed,
/// tells the <see cref="ICalendarEventObserver"/>s.
/// </summary>
public interface ICalendarEventManager
{
    /// <summary>
    /// Creates an unsaved event on a calendar with the event type's defaults.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="eventType">The event type's technical name, or <see langword="null"/> for the type's first event type.</param>
    /// <param name="startUtc">When the event starts, or <see langword="null"/> for the next whole hour.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The new event.</returns>
    Task<CalendarEvent> NewAsync(Calendar calendar, string eventType = null, DateTime? startUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an event by id.
    /// </summary>
    /// <param name="id">The event's id.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The event, or <see langword="null"/>.</returns>
    ValueTask<CalendarEvent> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the events a module owns for one of its records.
    /// </summary>
    /// <param name="source">The module's name.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<CalendarEvent>> FindBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an event by its iCalendar UID on a calendar.
    /// </summary>
    /// <param name="calendarId">The calendar's id.</param>
    /// <param name="uid">The UID.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The event, or <see langword="null"/>.</returns>
    Task<CalendarEvent> FindByUidAsync(string calendarId, string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and saves a new event.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> CreateAsync(CalendarEvent calendarEvent, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes an event (a whole series, for a repeating event). The change is applied, validated and saved; when it is
    /// refused the event is put back as it was.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="change">Applies the change.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> UpdateAsync(CalendarEvent calendarEvent, Action<CalendarEvent> change, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes one occurrence of a repeating event, that occurrence and every one after it, or the whole series.
    /// </summary>
    /// <param name="series">The repeating event.</param>
    /// <param name="originalStartUtc">When the occurrence was due to start by the rule, in UTC.</param>
    /// <param name="scope">Which occurrences the change applies to.</param>
    /// <param name="change">Applies the change to the occurrence (or to the series for <see cref="CalendarEditScope.AllOccurrences"/>).</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome, with the event that now holds the changed occurrence.</returns>
    Task<CalendarResult<CalendarEvent>> UpdateOccurrenceAsync(CalendarEvent series, DateTime originalStartUtc, CalendarEditScope scope, Action<CalendarEvent> change, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an event (a whole series, for a repeating event).
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> CancelAsync(CalendarEvent calendarEvent, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels one occurrence of a repeating event, or that occurrence and every one after it.
    /// </summary>
    /// <param name="series">The repeating event.</param>
    /// <param name="originalStartUtc">When the occurrence was due to start by the rule, in UTC.</param>
    /// <param name="scope">Which occurrences to cancel.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> CancelOccurrenceAsync(CalendarEvent series, DateTime originalStartUtc, CalendarEditScope scope, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an event, and every override of it for a repeating event.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the deletion is staged.</returns>
    Task DeleteAsync(CalendarEvent calendarEvent, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invites an attendee, or updates the attendee when already invited.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="attendee">The attendee.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> AddAttendeeAsync(CalendarEvent calendarEvent, CalendarAttendee attendee, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an attendee.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="attendeeKey">The attendee's key, from <see cref="CalendarAttendee.GetKey"/>.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> RemoveAttendeeAsync(CalendarEvent calendarEvent, string attendeeKey, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an attendee's answer.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="attendeeKey">The attendee's key, from <see cref="CalendarAttendee.GetKey"/>.</param>
    /// <param name="response">The answer.</param>
    /// <param name="options">How the change is made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<CalendarEvent>> RespondAsync(CalendarEvent calendarEvent, string attendeeKey, CalendarAttendeeResponse response, CalendarChangeOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks an event without saving it: the same validation and overlap check a save runs.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The errors, empty when the event could be saved.</returns>
    Task<IReadOnlyList<CalendarError>> ValidateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
}
