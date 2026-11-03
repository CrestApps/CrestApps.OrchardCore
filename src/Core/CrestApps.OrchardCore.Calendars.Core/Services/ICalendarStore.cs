using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Stores calendars. Write through <see cref="ICalendarService"/> so validation and handlers run.
/// </summary>
public interface ICalendarStore : ICatalog<Calendar>
{
    /// <summary>
    /// Finds the calendar a module manages for one of its records.
    /// </summary>
    /// <param name="source">The module's name.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    Task<Calendar> FindBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a calendar by the secret part of its feed address.
    /// </summary>
    /// <param name="feedToken">The token.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    Task<Calendar> FindByFeedTokenAsync(string feedToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of calendars, by name, and how many match in all.
    /// </summary>
    /// <param name="query">What to match.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<Calendar> Calendars)> PageAsync(CalendarListQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists calendars that match, by name, up to <see cref="CalendarListQuery.Limit"/>.
    /// </summary>
    /// <param name="query">What to match.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendars.</returns>
    Task<IReadOnlyList<Calendar>> ListAsync(CalendarListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the calendars shared with a user directly or through one of their roles, up to <paramref name="limit"/>.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="roles">The user's role names.</param>
    /// <param name="limit">The most calendars to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendars.</returns>
    Task<IReadOnlyList<Calendar>> ListSharedWithAsync(string userId, IEnumerable<string> roles, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the calendars every signed-in user can see (shared free/busy or public), up to <paramref name="limit"/>.
    /// </summary>
    /// <param name="limit">The most calendars to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendars.</returns>
    Task<IReadOnlyList<Calendar>> ListVisibleToEveryoneAsync(int limit, CancellationToken cancellationToken = default);
}
