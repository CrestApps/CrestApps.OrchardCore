using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Reads occurrences from the event store, expanding repeating events and applying their overrides, then adds the occurrences
/// from every <see cref="ICalendarEventSource"/> when asked.
/// </summary>
public sealed class DefaultCalendarQueryService : ICalendarQueryService
{
    private readonly ICalendarEventStore _eventStore;
    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarAccessService _accessService;
    private readonly IEnumerable<ICalendarEventSource> _eventSources;
    private readonly CalendarOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarQueryService"/> class.
    /// </summary>
    /// <param name="eventStore">The event store.</param>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="accessService">The access service.</param>
    /// <param name="eventSources">The event sources other modules register.</param>
    /// <param name="options">The calendar options.</param>
    /// <param name="logger">The logger.</param>
    public DefaultCalendarQueryService(
        ICalendarEventStore eventStore,
        ICalendarStore calendarStore,
        ICalendarAccessService accessService,
        IEnumerable<ICalendarEventSource> eventSources,
        IOptions<CalendarOptions> options,
        ILogger<DefaultCalendarQueryService> logger)
    {
        _eventStore = eventStore;
        _calendarStore = calendarStore;
        _accessService = accessService;
        _eventSources = eventSources;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var fromUtc = DateTime.SpecifyKind(query.FromUtc, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(query.ToUtc, DateTimeKind.Utc);

        if (toUtc <= fromUtc)
        {
            return [];
        }

        if ((toUtc - fromUtc).TotalDays > _options.MaxQueryDays)
        {
            toUtc = fromUtc.AddDays(_options.MaxQueryDays);
        }

        var events = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);

        if (query.CalendarIds.Count > 0)
        {
            Add(events, await _eventStore.ListInRangeAsync(query.CalendarIds, fromUtc, toUtc, _options.MaxEventsPerQuery, cancellationToken));
        }

        if (query.AttendeeUserIds.Count > 0 || query.ResourceCalendarIds.Count > 0)
        {
            Add(events, await _eventStore.ListAttendingInRangeAsync(query.AttendeeUserIds, query.ResourceCalendarIds, fromUtc, toUtc, _options.MaxEventsPerQuery, cancellationToken));
        }

        var seriesIds = events.Values
            .Where(calendarEvent => calendarEvent.SeriesId is null && CalendarEventExpander.TryGetRule(calendarEvent, out _))
            .Select(calendarEvent => calendarEvent.ItemId)
            .ToList();

        if (seriesIds.Count > 0)
        {
            Add(events, await _eventStore.ListOverridesAsync(seriesIds, cancellationToken));
        }

        var overridesBySeries = events.Values
            .Where(calendarEvent => calendarEvent.SeriesId is not null && calendarEvent.OriginalStartUtc.HasValue)
            .GroupBy(calendarEvent => calendarEvent.SeriesId)
            .ToDictionary(group => group.Key, group => new HashSet<DateTime>(group.Select(calendarEvent => calendarEvent.OriginalStartUtc.Value)), StringComparer.Ordinal);

        var occurrences = new List<CalendarOccurrence>();

        foreach (var calendarEvent in events.Values)
        {
            if (!Matches(calendarEvent, query))
            {
                continue;
            }

            if (calendarEvent.SeriesId is not null)
            {
                if (events.TryGetValue(calendarEvent.SeriesId, out var series) && series.Status == CalendarEventStatus.Cancelled && !query.IncludeCancelled)
                {
                    continue;
                }

                if (calendarEvent.StartUtc < toUtc && (calendarEvent.EndUtc > fromUtc || calendarEvent.EndUtc == calendarEvent.StartUtc && calendarEvent.StartUtc >= fromUtc))
                {
                    occurrences.Add(new CalendarOccurrence
                    {
                        Event = calendarEvent,
                        SeriesId = calendarEvent.SeriesId,
                        OriginalStartUtc = calendarEvent.OriginalStartUtc ?? calendarEvent.StartUtc,
                        Start = calendarEvent.Start,
                        End = calendarEvent.End,
                        StartUtc = calendarEvent.StartUtc,
                        EndUtc = calendarEvent.EndUtc,
                        IsRecurring = true,
                        IsOverride = true,
                    });
                }

                continue;
            }

            overridesBySeries.TryGetValue(calendarEvent.ItemId, out var overridden);

            occurrences.AddRange(CalendarEventExpander.Expand(calendarEvent, fromUtc, toUtc, overridden, _options.MaxOccurrencesPerQuery));
        }

        if (query.Viewer is not null)
        {
            occurrences = await ApplyAccessAsync(occurrences, query);
        }

        if (query.IncludeEventSources)
        {
            foreach (var source in _eventSources)
            {
                try
                {
                    occurrences.AddRange(await source.GetOccurrencesAsync(query, cancellationToken));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The calendar event source '{Source}' failed to return occurrences.", source.Name);
                }
            }
        }

        return occurrences
            .OrderBy(occurrence => occurrence.StartUtc)
            .ThenBy(occurrence => occurrence.EndUtc)
            .Take(_options.MaxOccurrencesPerQuery)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CalendarOccurrence> FindOccurrenceAsync(CalendarEvent calendarEvent, DateTime? originalStartUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (!CalendarEventExpander.TryGetRule(calendarEvent, out _) || !originalStartUtc.HasValue)
        {
            return CalendarEventExpander.Expand(calendarEvent, calendarEvent.StartUtc, calendarEvent.EndUtc.AddTicks(1)).FirstOrDefault()
                ?? new CalendarOccurrence
                {
                    Event = calendarEvent,
                    SeriesId = calendarEvent.SeriesId ?? calendarEvent.ItemId,
                    OriginalStartUtc = calendarEvent.OriginalStartUtc ?? calendarEvent.StartUtc,
                    Start = calendarEvent.Start,
                    End = calendarEvent.End,
                    StartUtc = calendarEvent.StartUtc,
                    EndUtc = calendarEvent.EndUtc,
                    IsRecurring = calendarEvent.SeriesId is not null,
                    IsOverride = calendarEvent.SeriesId is not null,
                };
        }

        var original = DateTime.SpecifyKind(originalStartUtc.Value, DateTimeKind.Utc);
        var overrides = await _eventStore.ListOverridesAsync([calendarEvent.ItemId], cancellationToken);
        var match = overrides.FirstOrDefault(item => item.OriginalStartUtc == original);

        if (match is not null)
        {
            return new CalendarOccurrence
            {
                Event = match,
                SeriesId = calendarEvent.ItemId,
                OriginalStartUtc = original,
                Start = match.Start,
                End = match.End,
                StartUtc = match.StartUtc,
                EndUtc = match.EndUtc,
                IsRecurring = true,
                IsOverride = true,
            };
        }

        return CalendarEventExpander.Expand(calendarEvent, original, original.AddSeconds(1), maxOccurrences: 1)
            .FirstOrDefault(occurrence => occurrence.OriginalStartUtc == original);
    }

    private static bool Matches(CalendarEvent calendarEvent, CalendarOccurrenceQuery query)
    {
        if (!query.IncludeCancelled && calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            return false;
        }

        if (!query.IncludeFree && calendarEvent.Transparency == CalendarTransparency.Free)
        {
            return false;
        }

        if (query.EventTypes.Count > 0 && !query.EventTypes.Contains(calendarEvent.EventType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.Sources.Count > 0 && !query.Sources.Contains(calendarEvent.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private async Task<List<CalendarOccurrence>> ApplyAccessAsync(List<CalendarOccurrence> occurrences, CalendarOccurrenceQuery query)
    {
        var calendarIds = occurrences.Select(occurrence => occurrence.Event.CalendarId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var calendars = calendarIds.Count > 0 ? await _calendarStore.GetAsync(calendarIds) : [];
        var levels = new Dictionary<string, CalendarAccessLevel>(StringComparer.Ordinal);

        foreach (var calendar in calendars)
        {
            levels[calendar.ItemId] = await _accessService.GetAccessLevelAsync(query.Viewer, calendar);
        }

        var viewerId = DefaultCalendarAccessService.GetUserId(query.Viewer);
        var result = new List<CalendarOccurrence>(occurrences.Count);

        foreach (var occurrence in occurrences)
        {
            var invited = !string.IsNullOrEmpty(viewerId) && occurrence.Event.Attendees.Any(attendee => attendee.UserId == viewerId);
            var level = levels.GetValueOrDefault(occurrence.Event.CalendarId);

            if (invited)
            {
                result.Add(occurrence);

                continue;
            }

            if (level == CalendarAccessLevel.None)
            {
                continue;
            }

            occurrence.BusyOnly = level == CalendarAccessLevel.FreeBusy ||
                (occurrence.Event.Visibility == CalendarEventVisibility.Private && level < CalendarAccessLevel.Write);

            result.Add(occurrence);
        }

        return result;
    }

    private static void Add(Dictionary<string, CalendarEvent> target, IEnumerable<CalendarEvent> events)
    {
        foreach (var calendarEvent in events)
        {
            target.TryAdd(calendarEvent.ItemId, calendarEvent);
        }
    }
}
