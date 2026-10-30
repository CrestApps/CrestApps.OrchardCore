using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// An external calendar provider. The synchronization service asks it for the external calendar's events and keeps the
/// calendar in step with them. Register providers as <see cref="ICalendarProvider"/> services.
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
    /// Reads the external calendar's events.
    /// </summary>
    /// <param name="calendar">The calendar being synchronized.</param>
    /// <param name="subscription">The subscription.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default);
}
