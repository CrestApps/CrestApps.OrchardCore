using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Works out when calendars, users and resources are busy or free, from their events and every
/// <see cref="ICalendarBusyTimeSource"/>.
/// </summary>
public interface ICalendarAvailabilityService
{
    /// <summary>
    /// Gets the busy time in a range. Intervals are not merged, so each says where it comes from.
    /// </summary>
    /// <param name="query">Whose busy time to read.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The busy intervals, ordered by start.</returns>
    Task<IReadOnlyList<CalendarBusyInterval>> GetBusyAsync(CalendarBusyQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds free windows of at least a given length in which everyone in the query is free.
    /// </summary>
    /// <param name="query">Whose time to check, and the range.</param>
    /// <param name="duration">How long the window must be.</param>
    /// <param name="within">Windows to search in, such as working hours, or <see langword="null"/> for the whole range.</param>
    /// <param name="maxResults">The most windows to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The free windows, ordered by start.</returns>
    Task<IReadOnlyList<CalendarTimeWindow>> FindFreeWindowsAsync(CalendarBusyQuery query, TimeSpan duration, IEnumerable<CalendarTimeWindow> within = null, int maxResults = 20, CancellationToken cancellationToken = default);
}
