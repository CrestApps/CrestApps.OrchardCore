using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Keeps the categories events are put in: each user's own, and the ones everyone can use.
/// </summary>
public interface ICalendarCategoryService
{
    /// <summary>
    /// Lists the categories a user can use: theirs and the ones everyone can use, by name.
    /// </summary>
    /// <param name="userId">The user, or <see langword="null"/> for only the shared ones.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The categories.</returns>
    Task<IReadOnlyList<CalendarCategory>> ListAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every category, everyone's, for exporting them.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The categories.</returns>
    Task<IReadOnlyList<CalendarCategory>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets categories by id.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The categories found.</returns>
    Task<IReadOnlyList<CalendarCategory>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a category.
    /// </summary>
    /// <param name="id">The category's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The category, or <see langword="null"/>.</returns>
    Task<CalendarCategory> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or changes a category.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CalendarCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a category. Its events keep their category id and are shown without a category.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(CalendarCategory category, CancellationToken cancellationToken = default);
}
