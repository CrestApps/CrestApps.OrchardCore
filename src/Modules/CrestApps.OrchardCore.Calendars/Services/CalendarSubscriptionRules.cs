using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The rules of a calendar's Subscription card: which calendars can subscribe to an external calendar, which addresses are
/// accepted, and how a saved subscription is stored.
/// </summary>
internal static class CalendarSubscriptionRules
{
    /// <summary>
    /// The shortest time between two synchronizations, in minutes.
    /// </summary>
    public const int MinIntervalMinutes = 5;

    /// <summary>
    /// The longest time between two synchronizations, in minutes (a week).
    /// </summary>
    public const int MaxIntervalMinutes = 10080;

    /// <summary>
    /// Gets whether a calendar can subscribe to an external calendar: no module manages it and its type allows external sync.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    public static bool Supports(Calendar calendar, ICalendarTypeManager typeManager)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(typeManager);

        return string.IsNullOrEmpty(calendar.Source) && (typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.ExternalSync) ?? false);
    }

    /// <summary>
    /// Gets whether an address starts with a scheme a subscription accepts: https://, http:// or webcal://.
    /// </summary>
    /// <param name="url">The address.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool HasAllowedScheme(string url)
    {
        return !string.IsNullOrEmpty(url) &&
            (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Stores a subscription on a calendar. Changing the provider or the address starts the synchronization over; saving again
    /// is how someone says a disconnected account was reconnected. The caller saves the calendar.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="providerName">The provider's name.</param>
    /// <param name="provider">The provider, or <see langword="null"/> when it is not available.</param>
    /// <param name="url">The address, or <see langword="null"/> for a provider that uses none.</param>
    /// <param name="intervalMinutes">The time between two synchronizations, in minutes.</param>
    /// <param name="twoWay">Whether local changes are written back.</param>
    /// <param name="conflictPolicy">Which copy wins an event changed on both sides.</param>
    /// <param name="enabled">Whether the subscription should run.</param>
    /// <returns>The stored subscription.</returns>
    public static CalendarSubscription Apply(
        Calendar calendar,
        string providerName,
        ICalendarProvider provider,
        string url,
        int intervalMinutes,
        bool twoWay,
        CalendarSyncConflictPolicy conflictPolicy,
        bool enabled)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var subscription = calendar.GetOrCreate<CalendarSubscription>();

        if (!string.Equals(subscription.Url, url, StringComparison.Ordinal) || !string.Equals(subscription.Provider, providerName, StringComparison.OrdinalIgnoreCase))
        {
            subscription.LastAttemptUtc = null;
            subscription.Failures = 0;
            subscription.SyncToken = null;
            subscription.PendingDeletes.Clear();
        }

        subscription.Provider = providerName;
        subscription.Url = string.IsNullOrEmpty(url) ? null : url;
        subscription.IntervalMinutes = Math.Clamp(intervalMinutes, MinIntervalMinutes, MaxIntervalMinutes);
        subscription.TwoWay = twoWay && provider is IWritableCalendarProvider;
        subscription.ConflictPolicy = conflictPolicy;
        subscription.Enabled = enabled && (provider?.IsConfigured(calendar, subscription) ?? false);

        // Saving the subscription again is how someone says the account was reconnected.
        if (subscription.NeedsReconnect)
        {
            subscription.NeedsReconnect = false;
            subscription.LastAttemptUtc = null;
            subscription.Failures = 0;
        }

        calendar.Put(subscription);

        return subscription;
    }
}
