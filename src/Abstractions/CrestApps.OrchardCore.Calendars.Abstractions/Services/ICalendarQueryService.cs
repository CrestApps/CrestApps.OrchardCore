using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Reads the occurrences in a range: one-time events, the expanded occurrences of repeating events with their changes, and the
/// occurrences other modules contribute through <see cref="ICalendarEventSource"/>.
/// </summary>
public interface ICalendarQueryService
{
    /// <summary>
    /// Gets the occurrences that overlap a range, ordered by start.
    /// </summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The occurrences.</returns>
    Task<IReadOnlyList<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one occurrence of an event.
    /// </summary>
    /// <param name="calendarEvent">The event or series.</param>
    /// <param name="originalStartUtc">When the occurrence was due to start, or <see langword="null"/> for a one-time event.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The occurrence, or <see langword="null"/> when the series has no occurrence then.</returns>
    Task<CalendarOccurrence> FindOccurrenceAsync(CalendarEvent calendarEvent, DateTime? originalStartUtc, CancellationToken cancellationToken = default);
}
