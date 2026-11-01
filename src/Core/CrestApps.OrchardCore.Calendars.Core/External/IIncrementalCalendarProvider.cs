using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// A provider that can read only what changed since the last read, with a sync token the external calendar hands out (a Google
/// Calendar <c>nextSyncToken</c>, a Microsoft Graph delta link). The synchronization service keeps the token on the
/// subscription, falls back to <see cref="ICalendarProvider.PullAsync"/> when there is none, and starts over with a full read
/// when the provider says the token expired.
/// </summary>
public interface IIncrementalCalendarProvider : ICalendarProvider
{
    /// <summary>
    /// Reads what changed since <paramref name="syncToken"/>.
    /// </summary>
    /// <param name="calendar">The calendar being synchronized.</param>
    /// <param name="subscription">The subscription.</param>
    /// <param name="syncToken">The token from the last read.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The changes and the token for the next read.</returns>
    Task<CalendarProviderChanges> PullChangesAsync(Calendar calendar, CalendarSubscription subscription, string syncToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the token to read changes from after a full read, or <see langword="null"/> when the full read can't give one.
    /// </summary>
    /// <param name="calendar">The calendar being synchronized.</param>
    /// <param name="subscription">The subscription.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The token.</returns>
    Task<string> GetSyncTokenAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default);
}
