using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Contributes busy time that isn't stored as events, such as a module's own reservations, so free/busy and scheduling see it.
/// Register it with <see cref="CalendarServiceCollectionExtensions.AddCalendarBusyTimeSource{TSource}"/>.
/// </summary>
public interface ICalendarBusyTimeSource
{
    /// <summary>
    /// Gets the source's name, recorded on each interval it returns.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the busy time in a range for the calendars and users asked for.
    /// </summary>
    /// <param name="query">Whose busy time, and when.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The busy intervals.</returns>
    Task<IEnumerable<CalendarBusyInterval>> GetBusyTimeAsync(CalendarBusyQuery query, CancellationToken cancellationToken = default);
}
