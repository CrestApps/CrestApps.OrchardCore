using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Models;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Raises the "starting soon" and "completed" moments of occurrences, exactly once each: a raised reminder is recorded under a
/// key made of its kind, the event, the occurrence's start and the offset, so a moved occurrence gets new reminders and its old
/// ones can never fire.
/// </summary>
public sealed class CalendarReminderService
{
    /// <summary>
    /// How far back a run looks, so a minute missed while the site was down or busy is still raised.
    /// </summary>
    public static readonly TimeSpan Lookback = TimeSpan.FromMinutes(15);

    private const int MaxEvents = 2000;

    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarStore _calendarStore;
    private readonly IEnumerable<ICalendarReminderOffsetProvider> _offsetProviders;
    private readonly IEnumerable<ICalendarReminderObserver> _observers;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReminderService"/> class.
    /// </summary>
    /// <param name="queryService">The occurrence query service.</param>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="offsetProviders">The providers of "starting soon" offsets.</param>
    /// <param name="observers">The reminder observers.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CalendarReminderService(
        ICalendarQueryService queryService,
        ICalendarStore calendarStore,
        IEnumerable<ICalendarReminderOffsetProvider> offsetProviders,
        IEnumerable<ICalendarReminderObserver> observers,
        ISession session,
        IClock clock,
        ILogger<CalendarReminderService> logger)
    {
        _queryService = queryService;
        _calendarStore = calendarStore;
        _offsetProviders = offsetProviders;
        _observers = observers;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Raises every reminder that is due and wasn't raised yet.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>How many reminders were raised.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_observers.Any())
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var offsets = new HashSet<TimeSpan>();

        foreach (var provider in _offsetProviders)
        {
            foreach (var offset in await provider.GetOffsetsAsync())
            {
                if (offset >= TimeSpan.Zero && offset <= TimeSpan.FromDays(30))
                {
                    offsets.Add(offset);
                }
            }
        }

        var maxOffset = offsets.Count == 0 ? TimeSpan.Zero : offsets.Max();
        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AllCalendars = true,
            FromUtc = now - Lookback - TimeSpan.FromDays(1),
            ToUtc = now + maxOffset + TimeSpan.FromMinutes(1),
        }, cancellationToken);

        var calendars = new Dictionary<string, Calendar>(StringComparer.Ordinal);
        var raised = 0;

        foreach (var occurrence in occurrences.Where(occurrence => !string.IsNullOrEmpty(occurrence.Event.ItemId)))
        {
            foreach (var offset in offsets)
            {
                var due = occurrence.StartUtc - offset;

                if (due <= now && due > now - Lookback && occurrence.StartUtc > now - Lookback &&
                    await TryRecordAsync(Key("start", occurrence, offset), occurrence, now))
                {
                    var context = await CreateContextAsync(occurrence, offset, calendars);

                    await NotifyAsync(observer => observer.OnStartingSoonAsync(context), occurrence);
                    raised++;
                }
            }

            if (occurrence.EndUtc <= now && occurrence.EndUtc > now - Lookback &&
                await TryRecordAsync(Key("end", occurrence, TimeSpan.Zero), occurrence, now))
            {
                var context = await CreateContextAsync(occurrence, TimeSpan.Zero, calendars);

                await NotifyAsync(observer => observer.OnCompletedAsync(context), occurrence);
                raised++;
            }
        }

        return raised;
    }

    /// <summary>
    /// Deletes reminder records older than a cutoff, which can never be needed again.
    /// </summary>
    /// <param name="olderThanUtc">The cutoff.</param>
    /// <param name="limit">The most records to delete.</param>
    /// <returns>How many were deleted.</returns>
    public async Task<int> PurgeAsync(DateTime olderThanUtc, int limit = 500)
    {
        var logs = await _session.Query<CalendarReminderLog, CalendarReminderLogIndex>(index => index.RaisedUtc < olderThanUtc, collection: CalendarsConstants.CollectionName)
            .Take(limit)
            .ListAsync();

        var count = 0;

        foreach (var log in logs)
        {
            _session.Delete(log, CalendarsConstants.CollectionName);
            count++;
        }

        return count;
    }

    internal static string Key(string kind, CalendarOccurrence occurrence, TimeSpan offset)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{kind}:{occurrence.Event.ItemId}:{occurrence.StartUtc:yyyyMMddTHHmmss}:{(int)offset.TotalMinutes}");
    }

    private async Task<bool> TryRecordAsync(string key, CalendarOccurrence occurrence, DateTime now)
    {
        var existing = await _session.QueryIndex<CalendarReminderLogIndex>(index => index.Key == key, collection: CalendarsConstants.CollectionName).FirstOrDefaultAsync();

        if (existing is not null)
        {
            return false;
        }

        await _session.SaveAsync(new CalendarReminderLog
        {
            ItemId = CrestApps.Core.UniqueId.GenerateId(),
            Key = key,
            EventId = occurrence.Event.ItemId,
            RaisedUtc = now,
        }, false, CalendarsConstants.CollectionName);

        return true;
    }

    private async Task<CalendarReminderContext> CreateContextAsync(CalendarOccurrence occurrence, TimeSpan offset, Dictionary<string, Calendar> calendars)
    {
        var calendarId = occurrence.Event.CalendarId ?? string.Empty;

        if (!calendars.TryGetValue(calendarId, out var calendar))
        {
            calendar = await _calendarStore.FindByIdAsync(calendarId);
            calendars[calendarId] = calendar;
        }

        return new CalendarReminderContext
        {
            Occurrence = occurrence,
            Calendar = calendar,
            Offset = offset,
        };
    }

    private async Task NotifyAsync(Func<ICalendarReminderObserver, Task> notify, CalendarOccurrence occurrence)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A calendar reminder observer ({Observer}) failed for the event '{EventId}'.", observer.GetType().Name, occurrence.Event.ItemId);
            }
        }
    }
}
