using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Works out busy and free time from events and every <see cref="ICalendarBusyTimeSource"/>.
/// </summary>
public sealed class DefaultCalendarAvailabilityService : ICalendarAvailabilityService
{
    private const string CalendarSource = "Calendar";

    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IEnumerable<ICalendarBusyTimeSource> _busyTimeSources;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarAvailabilityService"/> class.
    /// </summary>
    /// <param name="queryService">The occurrence query service.</param>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="busyTimeSources">The busy time sources other modules register.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCalendarAvailabilityService(
        ICalendarQueryService queryService,
        ICalendarStore calendarStore,
        ICalendarTypeManager typeManager,
        IEnumerable<ICalendarBusyTimeSource> busyTimeSources,
        ILogger<DefaultCalendarAvailabilityService> logger)
    {
        _queryService = queryService;
        _calendarStore = calendarStore;
        _typeManager = typeManager;
        _busyTimeSources = busyTimeSources;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarBusyInterval>> GetBusyAsync(CalendarBusyQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = new List<CalendarBusyInterval>();

        if (query.ToUtc <= query.FromUtc)
        {
            return result;
        }

        if (query.CalendarIds.Count > 0)
        {
            var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = query.CalendarIds,
                ResourceCalendarIds = query.CalendarIds,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                IncludeFree = true,
            }, cancellationToken);

            foreach (var occurrence in occurrences)
            {
                var calendarId = query.CalendarIds.Contains(occurrence.Event.CalendarId)
                    ? occurrence.Event.CalendarId
                    : occurrence.Event.Attendees.FirstOrDefault(attendee => attendee.ResourceCalendarId is not null && query.CalendarIds.Contains(attendee.ResourceCalendarId))?.ResourceCalendarId;

                var isResourceBooking = calendarId != occurrence.Event.CalendarId;

                if (!isResourceBooking && occurrence.Event.Transparency == CalendarTransparency.Free)
                {
                    continue;
                }

                Add(result, occurrence, query, calendarId, null);
            }
        }

        foreach (var userId in query.UserIds.Where(id => !string.IsNullOrEmpty(id)).Distinct())
        {
            var owned = await _calendarStore.ListAsync(new CalendarListQuery
            {
                OwnerId = userId,
                Limit = 100,
            }, cancellationToken);

            var calendarIds = owned
                .Where(calendar => calendar.OwnerKind == CalendarOwnerKind.User && (_typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.FreeBusy) ?? false))
                .Select(calendar => calendar.ItemId)
                .ToList();

            var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = calendarIds,
                AttendeeUserIds = [userId],
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                IncludeFree = false,
            }, cancellationToken);

            foreach (var occurrence in occurrences)
            {
                var attendee = occurrence.Event.Attendees.FirstOrDefault(item => item.UserId == userId);

                if (attendee?.Response == CalendarAttendeeResponse.Declined)
                {
                    continue;
                }

                Add(result, occurrence, query, occurrence.Event.CalendarId, userId);
            }
        }

        if (query.IncludeBusyTimeSources)
        {
            foreach (var source in _busyTimeSources)
            {
                try
                {
                    foreach (var interval in await source.GetBusyTimeAsync(query, cancellationToken))
                    {
                        if (interval.EndUtc > query.FromUtc && interval.StartUtc < query.ToUtc && (query.IncludeTentative || !interval.Tentative))
                        {
                            interval.Source ??= source.Name;
                            result.Add(interval);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The calendar busy time source '{Source}' failed to return busy time.", source.Name);
                }
            }
        }

        return result.OrderBy(interval => interval.StartUtc).ThenBy(interval => interval.EndUtc).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarTimeWindow>> FindFreeWindowsAsync(CalendarBusyQuery query, TimeSpan duration, IEnumerable<CalendarTimeWindow> within = null, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (duration <= TimeSpan.Zero || maxResults < 1)
        {
            return [];
        }

        var busy = Merge(await GetBusyAsync(query, cancellationToken));
        var windows = (within ?? [new CalendarTimeWindow(query.FromUtc, query.ToUtc)])
            .Select(window => new CalendarTimeWindow(Max(window.StartUtc, query.FromUtc), Min(window.EndUtc, query.ToUtc)))
            .Where(window => window.EndUtc > window.StartUtc)
            .OrderBy(window => window.StartUtc);

        var result = new List<CalendarTimeWindow>();

        foreach (var window in windows)
        {
            foreach (var free in Subtract(window, busy))
            {
                if (free.Duration >= duration)
                {
                    result.Add(free);

                    if (result.Count >= maxResults)
                    {
                        return result;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Merges overlapping or touching intervals into windows.
    /// </summary>
    /// <param name="intervals">The intervals.</param>
    /// <returns>The merged windows, ordered by start.</returns>
    public static IReadOnlyList<CalendarTimeWindow> Merge(IEnumerable<CalendarBusyInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        var result = new List<CalendarTimeWindow>();

        foreach (var interval in intervals.Where(interval => interval.EndUtc > interval.StartUtc).OrderBy(interval => interval.StartUtc))
        {
            if (result.Count > 0 && interval.StartUtc <= result[result.Count - 1].EndUtc)
            {
                var last = result[result.Count - 1];

                result[result.Count - 1] = last with { EndUtc = Max(last.EndUtc, interval.EndUtc) };
            }
            else
            {
                result.Add(new CalendarTimeWindow(interval.StartUtc, interval.EndUtc));
            }
        }

        return result;
    }

    /// <summary>
    /// Removes busy windows from a window.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="busy">The merged busy windows, ordered by start.</param>
    /// <returns>The free parts of the window.</returns>
    public static IEnumerable<CalendarTimeWindow> Subtract(CalendarTimeWindow window, IReadOnlyList<CalendarTimeWindow> busy)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(busy);

        var cursor = window.StartUtc;

        foreach (var block in busy)
        {
            if (block.EndUtc <= cursor)
            {
                continue;
            }

            if (block.StartUtc >= window.EndUtc)
            {
                break;
            }

            if (block.StartUtc > cursor)
            {
                yield return new CalendarTimeWindow(cursor, block.StartUtc);
            }

            cursor = Max(cursor, block.EndUtc);
        }

        if (cursor < window.EndUtc)
        {
            yield return new CalendarTimeWindow(cursor, window.EndUtc);
        }
    }

    private static void Add(List<CalendarBusyInterval> result, CalendarOccurrence occurrence, CalendarBusyQuery query, string calendarId, string userId)
    {
        if (query.ExcludeEventIds.Contains(occurrence.Event.ItemId) || query.ExcludeEventIds.Contains(occurrence.SeriesId))
        {
            return;
        }

        var tentative = occurrence.Event.Status == CalendarEventStatus.Tentative;

        if (tentative && !query.IncludeTentative)
        {
            return;
        }

        result.Add(new CalendarBusyInterval
        {
            StartUtc = occurrence.StartUtc,
            EndUtc = occurrence.EndUtc,
            CalendarId = calendarId,
            UserId = userId,
            EventId = occurrence.Event.ItemId,
            Source = CalendarSource,
            Tentative = tentative,
        });
    }

    private static DateTime Max(DateTime first, DateTime second)
    {
        return first > second ? first : second;
    }

    private static DateTime Min(DateTime first, DateTime second)
    {
        return first < second ? first : second;
    }
}
