using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Keeps subscribed calendars in step with their external calendars. A failure is recorded on the subscription and retried
/// later, waiting longer after each failure; the calendar itself keeps working with the events it already has.
/// </summary>
public sealed class CalendarSyncService
{
    /// <summary>
    /// The source imported events are marked with.
    /// </summary>
    public const string Source = "External";

    /// <summary>
    /// The provider a subscription uses when it names none: an iCalendar address.
    /// </summary>
    public const string DefaultProvider = "ics";

    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarImporter _importer;
    private readonly IEnumerable<ICalendarProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarSyncService"/> class.
    /// </summary>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="importer">The iCalendar importer.</param>
    /// <param name="providers">The external calendar providers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CalendarSyncService(
        ICalendarStore calendarStore,
        ICalendarService calendarService,
        ICalendarImporter importer,
        IEnumerable<ICalendarProvider> providers,
        IClock clock,
        ILogger<CalendarSyncService> logger)
    {
        _calendarStore = calendarStore;
        _calendarService = calendarService;
        _importer = importer;
        _providers = providers;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Lists the providers.
    /// </summary>
    /// <returns>The providers.</returns>
    public IEnumerable<ICalendarProvider> ListProviders()
    {
        return _providers;
    }

    /// <summary>
    /// Synchronizes the calendars whose subscription is due.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>How many calendars were synchronized.</returns>
    public async Task<int> SyncDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var calendars = await _calendarStore.ListAsync(new CalendarListQuery { Enabled = true, Limit = 1000 }, cancellationToken);
        var count = 0;

        foreach (var calendar in calendars)
        {
            if (!calendar.TryGet<CalendarSubscription>(out var subscription) || !subscription.Enabled || string.IsNullOrWhiteSpace(subscription.Url))
            {
                continue;
            }

            var wait = TimeSpan.FromMinutes(Math.Max(5, subscription.IntervalMinutes) * Math.Pow(2, Math.Min(subscription.Failures, 5)));

            if (subscription.LastAttemptUtc.HasValue && subscription.LastAttemptUtc.Value + wait > now)
            {
                continue;
            }

            await SyncAsync(calendar, cancellationToken);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Synchronizes one calendar now.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>What the import did, or <see langword="null"/> when it failed.</returns>
    public async Task<ICalendarImportResult> SyncAsync(Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var subscription = calendar.GetOrCreate<CalendarSubscription>();
        var provider = _providers.FirstOrDefault(item => string.Equals(item.Name, subscription.Provider ?? DefaultProvider, StringComparison.OrdinalIgnoreCase));

        subscription.LastAttemptUtc = _clock.UtcNow;

        ICalendarImportResult result = null;

        try
        {
            if (provider is null)
            {
                throw new InvalidOperationException($"The calendar provider '{subscription.Provider}' is not available.");
            }

            var events = await provider.PullAsync(calendar, subscription, cancellationToken);

            result = await _importer.ImportAsync(calendar, events, new ICalendarImportOptions
            {
                Actor = Source,
                Source = Source,
                Editable = false,
                RemoveMissing = true,
                SkipConflictCheck = true,
                Notify = true,
            });

            subscription.LastSyncedUtc = _clock.UtcNow;
            subscription.LastError = null;
            subscription.Failures = 0;
            subscription.LastResult = $"{result.Created} added, {result.Updated} changed, {result.Removed} removed, {result.Unchanged} unchanged, {result.Errors.Count} refused";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Synchronizing the calendar '{CalendarId}' with its external calendar failed.", calendar.ItemId);
            subscription.LastError = ex.Message;
            subscription.Failures++;
        }

        calendar.Put(subscription);
        await _calendarService.UpdateAsync(calendar, new CalendarChangeOptions { Actor = Source }, cancellationToken);

        return result;
    }
}
