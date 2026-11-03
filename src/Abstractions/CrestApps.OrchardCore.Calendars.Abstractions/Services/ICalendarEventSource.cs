using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Contributes read-only occurrences to the calendar view from another module's records, without copying them into events.
/// Register it with <see cref="CalendarServiceCollectionExtensions.AddCalendarEventSource{TSource}"/>.
/// </summary>
public interface ICalendarEventSource
{
    /// <summary>
    /// Gets the source's name, used as the <see cref="CalendarEvent.Source"/> of the occurrences it returns.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the occurrences that overlap a range for the calendars and users asked for. Return events that are not editable.
    /// </summary>
    /// <param name="query">What is being read.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The occurrences.</returns>
    Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default);
}
