using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Keeps the reminders shown to users.
/// </summary>
public sealed class CalendarUserReminderStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarUserReminderStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public CalendarUserReminderStore(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Adds a reminder.
    /// </summary>
    /// <param name="reminder">The reminder.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task CreateAsync(CalendarUserReminder reminder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        reminder.ItemId ??= CrestApps.Core.UniqueId.GenerateId();

        return _session.SaveAsync(reminder, false, CalendarsConstants.CollectionName, cancellationToken);
    }

    /// <summary>
    /// Lists a user's reminders that are still shown and were made after a time, the oldest first.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="sinceUtc">The earliest reminder to list.</param>
    /// <param name="take">The most to list.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reminders.</returns>
    public async Task<IReadOnlyList<CalendarUserReminder>> ListShownAsync(string userId, DateTime sinceUtc, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        return [.. await _session.Query<CalendarUserReminder, CalendarUserReminderIndex>(index => index.UserId == userId && !index.Dismissed && index.CreatedUtc >= sinceUtc, collection: CalendarsConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)];
    }

    /// <summary>
    /// Finds a reminder.
    /// </summary>
    /// <param name="itemId">The reminder's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reminder, or <see langword="null"/>.</returns>
    public Task<CalendarUserReminder> FindAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return Task.FromResult<CalendarUserReminder>(null);
        }

        return _session.Query<CalendarUserReminder, CalendarUserReminderIndex>(index => index.ItemId == itemId, collection: CalendarsConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Marks a reminder dismissed, so it is no longer shown.
    /// </summary>
    /// <param name="reminder">The reminder.</param>
    /// <param name="utcNow">The time.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task DismissAsync(CalendarUserReminder reminder, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        reminder.DismissedUtc ??= utcNow;

        return _session.SaveAsync(reminder, false, CalendarsConstants.CollectionName, cancellationToken);
    }
}
