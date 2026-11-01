using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// An external calendar provider. The synchronization service asks it for the external calendar's events and keeps the
/// calendar in step with them. Register providers as <see cref="ICalendarProvider"/> services. A provider that can read only
/// what changed since the last read also implements <see cref="IIncrementalCalendarProvider"/>, and one that can write local
/// changes back implements <see cref="IWritableCalendarProvider"/>.
/// </summary>
public interface ICalendarProvider
{
    /// <summary>
    /// Gets the provider's technical name, stored on each subscription.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown for the provider.
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets whether the subscription is made with an address (<see cref="CalendarSubscription.Url"/>). A provider that connects
    /// through an account instead keeps its own settings on the calendar and shows its own editor.
    /// </summary>
    bool UsesAddress => true;

    /// <summary>
    /// Gets whether a subscription has what the provider needs to synchronize, such as an address or a connected account.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="subscription">The subscription.</param>
    /// <returns><see langword="true"/> when the calendar can be synchronized.</returns>
    bool IsConfigured(Calendar calendar, CalendarSubscription subscription)
        => !string.IsNullOrWhiteSpace(subscription?.Url);

    /// <summary>
    /// Reads all of the external calendar's events. Events the result leaves out are removed from the calendar.
    /// </summary>
    /// <param name="calendar">The calendar being synchronized.</param>
    /// <param name="subscription">The subscription.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default);
}
