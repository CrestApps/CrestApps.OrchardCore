using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Stores events. Write through <see cref="ICalendarEventManager"/> so validation, conflict checks and handlers run.
/// </summary>
public interface ICalendarEventStore : ICatalog<CalendarEvent>
{
    /// <summary>
    /// Lists the events on some calendars that can have occurrences in a range: one-time events that overlap it, repeating events
    /// whose series overlaps it, and overrides that overlap it.
    /// </summary>
    /// <param name="calendarIds">The calendars.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range (exclusive).</param>
    /// <param name="limit">The most events to load.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListInRangeAsync(IEnumerable<string> calendarIds, DateTime fromUtc, DateTime toUtc, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists events on any calendar that some users or resources are invited to, and haven't declined, that can have occurrences
    /// in a range.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="resourceCalendarIds">The resource calendars.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range (exclusive).</param>
    /// <param name="limit">The most events to load.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListAttendingInRangeAsync(IEnumerable<string> userIds, IEnumerable<string> resourceCalendarIds, DateTime fromUtc, DateTime toUtc, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the overrides of some repeating events.
    /// </summary>
    /// <param name="seriesIds">The repeating events.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The overrides.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListOverridesAsync(IEnumerable<string> seriesIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the events a module owns for one of its records.
    /// </summary>
    /// <param name="source">The module's name.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an event by its iCalendar UID on a calendar. Overrides are left out.
    /// </summary>
    /// <param name="calendarId">The calendar.</param>
    /// <param name="uid">The UID.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The event, or <see langword="null"/>.</returns>
    Task<CalendarEvent> FindByUidAsync(string calendarId, string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a page of a calendar's events, by start, for exports and deletions done in batches.
    /// </summary>
    /// <param name="calendarId">The calendar.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListByCalendarAsync(string calendarId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists events on any calendar that can have occurrences in a range, for background work such as reminders.
    /// </summary>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range (exclusive).</param>
    /// <param name="limit">The most events to load.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<CalendarEvent>> ListAnyInRangeAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken cancellationToken = default);
}
