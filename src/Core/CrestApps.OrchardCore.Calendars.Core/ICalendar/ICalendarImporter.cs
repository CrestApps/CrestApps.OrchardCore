using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Calendars.Core.ICalendar;

/// <summary>
/// Imports iCalendar events into a calendar, matching them to existing events by UID so importing the same file or feed again
/// updates instead of duplicating. Changed occurrences (a <c>RECURRENCE-ID</c>) become overrides of their series.
/// </summary>
public sealed class ICalendarImporter
{
    private const int PageSize = 500;

    private readonly ICalendarEventManager _eventManager;
    private readonly ICalendarEventStore _eventStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ICalendarImporter"/> class.
    /// </summary>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="eventStore">The event store.</param>
    public ICalendarImporter(
        ICalendarEventManager eventManager,
        ICalendarEventStore eventStore)
    {
        _eventManager = eventManager;
        _eventStore = eventStore;
    }

    /// <summary>
    /// Imports events into a calendar.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="events">The events read from the document.</param>
    /// <param name="options">How the import is made.</param>
    /// <returns>What the import did.</returns>
    public async Task<ICalendarImportResult> ImportAsync(Calendar calendar, IEnumerable<ICalendarEventData> events, ICalendarImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);

        var result = new ICalendarImportResult();
        var change = new CalendarChangeOptions
        {
            Actor = options.Actor,
            SkipConflictCheck = options.SkipConflictCheck,
            Notify = options.Notify,
            SendInvitations = false,
        };

        var existing = await LoadAsync(calendar.ItemId, options.Source);
        var masters = existing.Where(calendarEvent => calendarEvent.SeriesId is null).GroupBy(calendarEvent => calendarEvent.Uid ?? calendarEvent.ItemId).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = events.ToList();

        // Series first, so their overrides can find them.
        foreach (var data in list.OrderBy(item => item.RecurrenceIdUtc.HasValue ? 1 : 0))
        {
            if (data.RecurrenceIdUtc.HasValue)
            {
                if (!masters.TryGetValue(data.Uid, out var series))
                {
                    result.Errors.Add($"{data.Summary}: the repeating event it changes is not in the import.");

                    continue;
                }

                var key = data.Uid + "|" + data.RecurrenceIdUtc.Value.ToString("O");
                var current = existing.FirstOrDefault(item => item.SeriesId == series.ItemId && item.OriginalStartUtc == data.RecurrenceIdUtc.Value);

                seen.Add(current?.ItemId ?? key);

                if (current is null)
                {
                    var created = await _eventManager.UpdateOccurrenceAsync(series, data.RecurrenceIdUtc.Value, CalendarEditScope.ThisOccurrence, target => Apply(data, target, options, isOverride: true), change);

                    Count(result, created, data, isNew: true);
                }
                else
                {
                    var updated = await _eventManager.UpdateAsync(current, target => Apply(data, target, options, isOverride: true), change);

                    Count(result, updated, data, isNew: false);
                }

                continue;
            }

            if (masters.TryGetValue(data.Uid, out var match))
            {
                seen.Add(match.ItemId);

                if (match.Sequence == data.Sequence && data.LastModifiedUtc.HasValue && match.ModifiedUtc >= data.LastModifiedUtc.Value)
                {
                    result.Unchanged++;

                    continue;
                }

                var updated = await _eventManager.UpdateAsync(match, target => Apply(data, target, options, isOverride: false), change);

                Count(result, updated, data, isNew: false);
            }
            else
            {
                var calendarEvent = await _eventManager.NewAsync(calendar);

                calendarEvent.Uid = data.Uid;
                Apply(data, calendarEvent, options, isOverride: false);

                var created = await _eventManager.CreateAsync(calendarEvent, change);

                if (created.Succeeded)
                {
                    masters[data.Uid] = created.Value;
                    seen.Add(created.Value.ItemId);
                }

                Count(result, created, data, isNew: true);
            }
        }

        if (options.RemoveMissing)
        {
            foreach (var calendarEvent in existing.Where(item => item.SeriesId is null && !seen.Contains(item.ItemId)))
            {
                await _eventManager.DeleteAsync(calendarEvent, change);
                result.Removed++;
            }
        }

        return result;
    }

    private async Task<List<CalendarEvent>> LoadAsync(string calendarId, string source)
    {
        var events = new List<CalendarEvent>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _eventStore.ListByCalendarAsync(calendarId, skip, PageSize);

            events.AddRange(page.Where(calendarEvent => source is null || string.Equals(calendarEvent.Source, source, StringComparison.Ordinal)));

            if (page.Count < PageSize)
            {
                break;
            }
        }

        return events;
    }

    private static void Apply(ICalendarEventData data, CalendarEvent target, ICalendarImportOptions options, bool isOverride)
    {
        target.Title = string.IsNullOrWhiteSpace(data.Summary) ? "(no title)" : data.Summary;
        target.Description = data.Description;
        target.Location = data.Location;
        target.AllDay = data.AllDay;
        target.Start = data.Start;
        target.End = data.End ?? (data.AllDay ? data.Start.AddDays(1) : data.Start.AddHours(1));
        target.TimeZoneId = data.TimeZoneId ?? target.TimeZoneId;
        target.Status = data.Status;
        target.Transparency = data.Transparency;
        target.Sequence = data.Sequence;
        target.Source = options.Source ?? target.Source;
        target.Editable = options.Editable;
        target.Attendees = data.Attendees.ToList();

        if (!isOverride)
        {
            target.Recurrence = string.IsNullOrWhiteSpace(data.Rule)
                ? null
                : new CalendarRecurrence
                {
                    Rule = data.Rule,
                    ExceptionDates = data.ExceptionDates.ToList(),
                };
        }
    }

    private static void Count(ICalendarImportResult result, CalendarResult<CalendarEvent> outcome, ICalendarEventData data, bool isNew)
    {
        if (!outcome.Succeeded)
        {
            result.Errors.Add($"{data.Summary}: {string.Join(" ", outcome.Errors.Select(error => error.Message))}");
        }
        else if (isNew)
        {
            result.Created++;
        }
        else
        {
            result.Updated++;
        }
    }
}
