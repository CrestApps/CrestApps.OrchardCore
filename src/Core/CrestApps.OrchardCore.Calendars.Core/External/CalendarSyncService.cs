using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Keeps subscribed calendars in step with their external calendars: reads the external changes (all of them, or only what
/// changed since the last read for an <see cref="IIncrementalCalendarProvider"/>), and for a two-way subscription writes local
/// changes and deletions back through an <see cref="IWritableCalendarProvider"/>. An event changed on both sides is a conflict,
/// settled by the subscription's <see cref="CalendarSyncConflictPolicy"/>. A failure is recorded on the subscription and retried
/// later, waiting longer after each failure; the calendar itself keeps working with the events it already has.
/// </summary>
public sealed class CalendarSyncService
{
    /// <summary>
    /// The source imported events are marked with, and the actor of the changes synchronization makes.
    /// </summary>
    public const string Source = "External";

    /// <summary>
    /// The provider a subscription uses when it names none: an iCalendar address.
    /// </summary>
    public const string DefaultProvider = "ics";

    private const int PageSize = 500;
    private const int MaxListedCalendars = 1000;

    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventStore _eventStore;
    private readonly ICalendarEventManager _eventManager;
    private readonly ICalendarImporter _importer;
    private readonly IEnumerable<ICalendarProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarSyncService"/> class.
    /// </summary>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="eventStore">The event store.</param>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="importer">The iCalendar importer.</param>
    /// <param name="providers">The external calendar providers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CalendarSyncService(
        ICalendarStore calendarStore,
        ICalendarService calendarService,
        ICalendarEventStore eventStore,
        ICalendarEventManager eventManager,
        ICalendarImporter importer,
        IEnumerable<ICalendarProvider> providers,
        IClock clock,
        ILogger<CalendarSyncService> logger)
    {
        _calendarStore = calendarStore;
        _calendarService = calendarService;
        _eventStore = eventStore;
        _eventManager = eventManager;
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
    /// Finds a provider by name.
    /// </summary>
    /// <param name="name">The provider's name, or <see langword="null"/> for the default provider.</param>
    /// <returns>The provider, or <see langword="null"/> when none has the name.</returns>
    public ICalendarProvider FindProvider(string name)
    {
        return _providers.FirstOrDefault(item => string.Equals(item.Name, name ?? DefaultProvider, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Lists the calendars whose subscription is due: their interval passed since the last attempt (waiting twice as long after
    /// each failure in a row, up to 32 times), or they have local changes to write back.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The ids of the calendars to synchronize.</returns>
    public async Task<IReadOnlyList<string>> ListDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var calendars = await _calendarStore.ListAsync(new CalendarListQuery { Enabled = true, Limit = MaxListedCalendars }, cancellationToken);
        var due = new List<string>();

        foreach (var calendar in calendars)
        {
            if (!calendar.TryGet<CalendarSubscription>(out var subscription) || !subscription.Enabled || subscription.NeedsReconnect)
            {
                continue;
            }

            var provider = FindProvider(subscription.Provider);

            if (provider is not null && !provider.IsConfigured(calendar, subscription))
            {
                continue;
            }

            var backoff = Math.Pow(2, Math.Min(subscription.Failures, 5));
            var wait = subscription.PendingPush && subscription.Failures == 0
                ? TimeSpan.Zero
                : TimeSpan.FromMinutes((subscription.PendingPush ? 5 : Math.Max(5, subscription.IntervalMinutes)) * backoff);

            if (subscription.LastAttemptUtc.HasValue && subscription.LastAttemptUtc.Value + wait > now)
            {
                continue;
            }

            due.Add(calendar.ItemId);
        }

        return due;
    }

    /// <summary>
    /// Synchronizes the calendars whose subscription is due, one after the other. One calendar failing doesn't stop the others.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>How many calendars were synchronized.</returns>
    public async Task<int> SyncDueAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;

        foreach (var calendarId in await ListDueAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await SyncAsync(calendarId, cancellationToken);
                count++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Synchronizing the calendar '{CalendarId}' failed.", calendarId);
            }
        }

        return count;
    }

    /// <summary>
    /// Synchronizes one calendar now.
    /// </summary>
    /// <param name="calendarId">The calendar's id.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>What the run did, or <see langword="null"/> when the calendar is gone or the run failed.</returns>
    public async Task<ICalendarImportResult> SyncAsync(string calendarId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(calendarId);

        var calendar = await _calendarService.FindByIdAsync(calendarId, cancellationToken);

        return calendar is null ? null : await SyncAsync(calendar, cancellationToken);
    }

    /// <summary>
    /// Synchronizes one calendar now.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>What the run did, or <see langword="null"/> when it failed.</returns>
    public async Task<ICalendarImportResult> SyncAsync(Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var subscription = calendar.GetOrCreate<CalendarSubscription>();
        var provider = FindProvider(subscription.Provider);

        subscription.LastAttemptUtc = _clock.UtcNow;

        ICalendarImportResult result = null;

        try
        {
            if (provider is null)
            {
                throw new InvalidOperationException($"The calendar provider '{subscription.Provider}' is not available.");
            }

            var writer = subscription.TwoWay ? provider as IWritableCalendarProvider : null;

            var run = new SyncRun();

            result = run.Result = await PullAsync(calendar, subscription, provider, writer is not null, run, cancellationToken);

            if (writer is not null)
            {
                await PushAsync(calendar, subscription, writer, run, cancellationToken);
            }

            subscription.LastSyncedUtc = _clock.UtcNow;
            subscription.LastError = result.Errors.Count > 0 ? string.Join(" ", result.Errors.Take(3)) : null;
            subscription.Failures = 0;
            subscription.LastResult = $"{result.Created} added, {result.Updated} changed, {result.Removed} removed, {result.Unchanged} unchanged, {result.Conflicts} conflicts, {result.Errors.Count} refused";
        }
        catch (CalendarProviderAuthorizationException ex)
        {
            _logger.LogWarning(ex, "The external calendar of the calendar '{CalendarId}' refused access; synchronization is paused until it is reconnected.", calendar.ItemId);
            subscription.LastError = ex.Message;
            subscription.NeedsReconnect = true;
            subscription.Failures++;
            result = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Synchronizing the calendar '{CalendarId}' with its external calendar failed.", calendar.ItemId);
            subscription.LastError = ex.Message;
            subscription.Failures++;
            result = null;
        }

        calendar.Put(subscription);
        await _calendarService.UpdateAsync(calendar, new CalendarChangeOptions { Actor = Source }, cancellationToken);

        return result;
    }

    private async Task<ICalendarImportResult> PullAsync(Calendar calendar, CalendarSubscription subscription, ICalendarProvider provider, bool twoWay, SyncRun run, CancellationToken cancellationToken)
    {
        var incremental = provider as IIncrementalCalendarProvider;
        CalendarProviderChanges changes = null;

        if (incremental is not null && !string.IsNullOrEmpty(subscription.SyncToken))
        {
            changes = await incremental.PullChangesAsync(calendar, subscription, subscription.SyncToken, cancellationToken);

            if (changes.FullSyncRequired)
            {
                subscription.SyncToken = null;
                changes = null;
            }
        }

        var full = changes is null;
        IEnumerable<ICalendarEventData> events = full ? await provider.PullAsync(calendar, subscription, cancellationToken) : changes.Events;
        var conflicts = 0;

        var options = new ICalendarImportOptions
        {
            Actor = Source,
            Source = Source,
            Editable = twoWay,
            RemoveMissing = full,
            SkipConflictCheck = true,
            Notify = true,
            Applied = (target, data) => SetMapping(target, calendar, provider, data.Uid, data.Version),
        };

        if (twoWay)
        {
            options.CanUpdate = (local, data) =>
            {
                var mapping = FindMapping(local, calendar, provider);

                if (mapping is null || !IsChangedLocally(local, mapping))
                {
                    return true;
                }

                if (data.Version is not null && string.Equals(data.Version, mapping.Version, StringComparison.Ordinal))
                {
                    // Only the local copy changed: keep it, the push writes it back.
                    return false;
                }

                conflicts++;

                if (subscription.ConflictPolicy == CalendarSyncConflictPolicy.ExternalWins)
                {
                    return true;
                }

                run.Overwrite.Add(local.ItemId);

                return false;
            };

            options.CanRemove = local => !KeepsDeletedLocalChange(local, calendar, provider, subscription, run);
        }

        var result = await _importer.ImportAsync(calendar, events, options);

        if (!full)
        {
            foreach (var externalId in changes.DeletedIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
            {
                var local = await _eventStore.FindByUidAsync(calendar.ItemId, externalId, cancellationToken);

                if (local is null || local.SeriesId is not null || !string.Equals(local.Source, Source, StringComparison.Ordinal))
                {
                    continue;
                }

                if (twoWay && KeepsDeletedLocalChange(local, calendar, provider, subscription, run))
                {
                    continue;
                }

                await _eventManager.DeleteAsync(local, new CalendarChangeOptions { Actor = Source, SkipConflictCheck = true, SendInvitations = false }, cancellationToken);
                result.Removed++;
            }
        }

        result.Conflicts += conflicts;

        if (incremental is not null)
        {
            subscription.SyncToken = full
                ? await incremental.GetSyncTokenAsync(calendar, subscription, cancellationToken)
                : changes.NextSyncToken ?? subscription.SyncToken;
        }

        return result;
    }

    private async Task PushAsync(Calendar calendar, CalendarSubscription subscription, IWritableCalendarProvider provider, SyncRun run, CancellationToken cancellationToken)
    {
        var result = run.Result;

        foreach (var deleted in subscription.PendingDeletes.ToList())
        {
            var context = new CalendarProviderPushContext { Calendar = calendar, Subscription = subscription, Mapping = deleted };

            try
            {
                await provider.DeleteAsync(context, cancellationToken);
            }
            catch (CalendarProviderConflictException) when (subscription.ConflictPolicy == CalendarSyncConflictPolicy.LocalWins)
            {
                result.Conflicts++;
                deleted.Version = null;
                await provider.DeleteAsync(context, cancellationToken);
            }
            catch (CalendarProviderConflictException)
            {
                // The external copy changed after it was deleted here: it wins, so read it back in full next time.
                result.Conflicts++;
                subscription.SyncToken = null;
            }

            subscription.PendingDeletes.Remove(deleted);
        }

        var events = new List<CalendarEvent>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _eventStore.ListByCalendarAsync(calendar.ItemId, skip, PageSize, cancellationToken);

            events.AddRange(page.Where(calendarEvent => calendarEvent.Source is null || string.Equals(calendarEvent.Source, Source, StringComparison.Ordinal)));

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var byId = events.ToDictionary(calendarEvent => calendarEvent.ItemId, StringComparer.Ordinal);

        // Series first, so an occurrence change can point to its series' external copy.
        foreach (var calendarEvent in events.OrderBy(item => item.SeriesId is null ? 0 : 1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mapping = run.Recreate.Contains(calendarEvent.ItemId) ? null : FindMapping(calendarEvent, calendar, provider);

            if (mapping is not null && !IsChangedLocally(calendarEvent, mapping))
            {
                continue;
            }

            var seriesMapping = calendarEvent.SeriesId is not null && byId.TryGetValue(calendarEvent.SeriesId, out var series)
                ? FindMapping(series, calendar, provider)
                : null;

            if (calendarEvent.SeriesId is not null && seriesMapping is null)
            {
                // The series isn't in the external calendar yet; its occurrence change goes with it next time.
                continue;
            }

            var context = new CalendarProviderPushContext
            {
                Calendar = calendar,
                Subscription = subscription,
                Event = calendarEvent,
                Mapping = mapping is null ? null : Copy(mapping),
                SeriesMapping = seriesMapping,
            };

            if (context.Mapping is not null && run.Overwrite.Contains(calendarEvent.ItemId))
            {
                // The local copy already won a conflict on the read: overwrite the external copy.
                context.Mapping.Version = null;
            }

            CalendarProviderPushResult pushed;

            try
            {
                pushed = await provider.PushAsync(context, cancellationToken);
            }
            catch (CalendarProviderConflictException) when (subscription.ConflictPolicy == CalendarSyncConflictPolicy.LocalWins)
            {
                result.Conflicts++;
                context.Mapping.Version = null;
                pushed = await provider.PushAsync(context, cancellationToken);
            }
            catch (CalendarProviderConflictException)
            {
                // The external copy changed since the read: it wins, so read everything again next time.
                result.Conflicts++;
                subscription.SyncToken = null;

                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not CalendarProviderAuthorizationException)
            {
                _logger.LogWarning(ex, "Writing the event '{EventId}' to the external calendar of the calendar '{CalendarId}' failed.", calendarEvent.ItemId, calendar.ItemId);
                result.Errors.Add($"{calendarEvent.Title}: {ex.Message}");

                continue;
            }

            if (pushed is null || string.IsNullOrEmpty(pushed.ExternalEventId))
            {
                result.Errors.Add($"{calendarEvent.Title}: the provider returned no external id.");

                continue;
            }

            var updated = await _eventManager.UpdateAsync(calendarEvent, target =>
            {
                if (target.SeriesId is null)
                {
                    target.Uid = pushed.ExternalEventId;
                }

                target.Source = Source;
                target.Editable = true;
                SetMapping(target, calendar, provider, pushed.ExternalEventId, pushed.Version);
            }, new CalendarChangeOptions { Actor = Source, SkipConflictCheck = true, Notify = false, SendInvitations = false }, cancellationToken);

            if (!updated.Succeeded)
            {
                result.Errors.Add($"{calendarEvent.Title}: {string.Join(" ", updated.Errors.Select(error => error.Message))}");

                continue;
            }

            if (mapping is null)
            {
                result.Created++;
            }
            else
            {
                result.Updated++;
            }
        }

        subscription.PendingPush = false;
    }

    private static bool KeepsDeletedLocalChange(CalendarEvent local, Calendar calendar, ICalendarProvider provider, CalendarSubscription subscription, SyncRun run)
    {
        if (subscription.ConflictPolicy != CalendarSyncConflictPolicy.LocalWins)
        {
            return false;
        }

        var mapping = FindMapping(local, calendar, provider);

        if (mapping is null || !IsChangedLocally(local, mapping))
        {
            return false;
        }

        // The local change wins over the external deletion: the push creates the external copy again.
        run.Recreate.Add(local.ItemId);

        return true;
    }

    private void SetMapping(CalendarEvent target, Calendar calendar, ICalendarProvider provider, string externalId, string version)
    {
        target.ProviderMappings ??= [];

        var mapping = FindMapping(target, calendar, provider);

        if (mapping is null)
        {
            mapping = new CalendarProviderMapping { ConnectionId = calendar.ItemId, Provider = provider.Name };
            target.ProviderMappings.Add(mapping);
        }

        mapping.ExternalEventId = externalId;
        mapping.Version = version;
        mapping.ContentHash = CalendarSyncFingerprint.Compute(target);
        mapping.LastSyncedUtc = _clock.UtcNow;
    }

    /// <summary>
    /// Finds an event's external copy in a calendar's subscription.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="calendar">The subscribed calendar.</param>
    /// <param name="provider">The subscription's provider.</param>
    /// <returns>The mapping, or <see langword="null"/> when the event has no external copy yet.</returns>
    internal static CalendarProviderMapping FindMapping(CalendarEvent calendarEvent, Calendar calendar, ICalendarProvider provider)
    {
        return calendarEvent.ProviderMappings?.FirstOrDefault(mapping =>
            string.Equals(mapping.ConnectionId, calendar.ItemId, StringComparison.Ordinal) &&
            string.Equals(mapping.Provider, provider.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsChangedLocally(CalendarEvent calendarEvent, CalendarProviderMapping mapping)
    {
        return !string.Equals(mapping.ContentHash, CalendarSyncFingerprint.Compute(calendarEvent), StringComparison.Ordinal);
    }

    private static CalendarProviderMapping Copy(CalendarProviderMapping mapping)
    {
        return new CalendarProviderMapping
        {
            ConnectionId = mapping.ConnectionId,
            Provider = mapping.Provider,
            ExternalEventId = mapping.ExternalEventId,
            Version = mapping.Version,
            ContentHash = mapping.ContentHash,
            LastSyncedUtc = mapping.LastSyncedUtc,
        };
    }

    private sealed class SyncRun
    {
        public ICalendarImportResult Result { get; set; }

        // Events whose local change won a conflict on the read, to write over the external copy.
        public HashSet<string> Overwrite { get; } = new(StringComparer.Ordinal);

        // Events deleted externally whose local change wins, to create again.
        public HashSet<string> Recreate { get; } = new(StringComparer.Ordinal);
    }
}
