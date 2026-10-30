using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Creates, changes, deletes and finds calendars. Every write goes through here so validation and handlers always run.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Creates an unsaved calendar of a type, with the type's defaults.
    /// </summary>
    /// <param name="type">The calendar type's technical name.</param>
    /// <returns>The new calendar, or <see langword="null"/> when the type isn't registered.</returns>
    Task<Calendar> NewAsync(string type);

    /// <summary>
    /// Finds a calendar by id.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    ValueTask<Calendar> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds calendars by id.
    /// </summary>
    /// <param name="ids">The calendars' ids.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendars that exist.</returns>
    ValueTask<IReadOnlyCollection<Calendar>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the calendar a module manages for one of its records.
    /// </summary>
    /// <param name="source">The module's name.</param>
    /// <param name="sourceId">The record's id.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendar, or <see langword="null"/>.</returns>
    Task<Calendar> FindBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists calendars, bounded to <paramref name="take"/>, with how many match in all.
    /// </summary>
    /// <param name="query">What to match.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<Calendar> Calendars)> PageAsync(CalendarListQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every calendar that matches, for pickers and access checks. Bounded by <see cref="CalendarListQuery.Limit"/>.
    /// </summary>
    /// <param name="query">What to match.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The calendars, by name.</returns>
    Task<IReadOnlyList<Calendar>> ListAsync(CalendarListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and saves a new calendar.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="options">How the change is made.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<Calendar>> CreateAsync(Calendar calendar, CalendarChangeOptions options = null);

    /// <summary>
    /// Validates and saves changes to a calendar.
    /// </summary>
    /// <param name="calendar">The calendar, already changed.</param>
    /// <param name="options">How the change is made.</param>
    /// <returns>The outcome.</returns>
    Task<CalendarResult<Calendar>> UpdateAsync(Calendar calendar, CalendarChangeOptions options = null);

    /// <summary>
    /// Deletes a calendar and every event on it.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="options">How the change is made.</param>
    /// <returns>A task that completes when the deletion is staged.</returns>
    Task DeleteAsync(Calendar calendar, CalendarChangeOptions options = null);

    /// <summary>
    /// Gets a user's personal calendar, creating it when the user has none yet.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="userName">The user's name, used to name a new calendar.</param>
    /// <returns>The personal calendar, or <see langword="null"/> when the Personal type isn't registered.</returns>
    Task<Calendar> GetOrCreatePersonalCalendarAsync(string userId, string userName);
}
