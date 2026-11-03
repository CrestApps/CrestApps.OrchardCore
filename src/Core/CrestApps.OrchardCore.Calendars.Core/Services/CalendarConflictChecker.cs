using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Checks that an event doesn't push a calendar that limits overlapping events over its capacity, at any of its occurrences in
/// the next year.
/// </summary>
public sealed class CalendarConflictChecker
{
    private const int MaxCheckedOccurrences = 500;
    private const int HorizonDays = 366;

    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarEventStore _eventStore;
    private readonly ICalendarTypeManager _typeManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarConflictChecker"/> class.
    /// </summary>
    /// <param name="queryService">The occurrence query service.</param>
    /// <param name="eventStore">The event store.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarConflictChecker(
        ICalendarQueryService queryService,
        ICalendarEventStore eventStore,
        ICalendarTypeManager typeManager,
        IStringLocalizer<CalendarConflictChecker> stringLocalizer)
    {
        _queryService = queryService;
        _eventStore = eventStore;
        _typeManager = typeManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks an event against calendars that limit overlapping events.
    /// </summary>
    /// <param name="calendarEvent">The event, with its UTC times computed.</param>
    /// <param name="targets">The calendars whose limit the event counts toward.</param>
    /// <returns>An error per calendar the event would overfill, empty when it fits.</returns>
    public async Task<IReadOnlyList<CalendarError>> CheckAsync(CalendarEvent calendarEvent, IEnumerable<Calendar> targets)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(targets);

        var targetList = targets.Where(target => target.ConflictPolicy == CalendarConflictPolicy.LimitConcurrent).ToList();

        if (targetList.Count == 0 || calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            return [];
        }

        var horizon = calendarEvent.StartUtc.AddDays(HorizonDays);
        var overridden = new HashSet<DateTime>();

        if (calendarEvent.SeriesId is null && CalendarEventExpander.TryGetRule(calendarEvent, out _))
        {
            foreach (var item in await _eventStore.ListOverridesAsync([calendarEvent.ItemId]))
            {
                if (item.OriginalStartUtc.HasValue)
                {
                    overridden.Add(item.OriginalStartUtc.Value);
                }
            }
        }

        var occurrences = CalendarEventExpander.Expand(calendarEvent, calendarEvent.StartUtc, horizon, overridden, MaxCheckedOccurrences).ToList();

        if (occurrences.Count == 0)
        {
            return [];
        }

        var fromUtc = occurrences.Min(occurrence => occurrence.StartUtc);
        var toUtc = occurrences.Max(occurrence => occurrence.EndUtc);

        if (toUtc <= fromUtc)
        {
            toUtc = fromUtc.AddMinutes(1);
        }

        var errors = new List<CalendarError>();

        foreach (var target in targetList)
        {
            var existing = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = [target.ItemId],
                ResourceCalendarIds = [target.ItemId],
                FromUtc = fromUtc,
                ToUtc = toUtc,
            });

            var counted = existing.Where(occurrence => !IsSelf(occurrence, calendarEvent) && Counts(occurrence, target)).ToList();

            foreach (var occurrence in occurrences)
            {
                if (GetPeak(counted, occurrence.StartUtc, occurrence.EndUtc) + 1 > target.Capacity)
                {
                    var local = CalendarTime.ToLocal(occurrence.StartUtc, target.TimeZoneId ?? calendarEvent.TimeZoneId);

                    errors.Add(new CalendarError(
                        nameof(CalendarEvent.Start),
                        target.Capacity == 1
                            ? S["{0} is already booked at {1}.", target.Name, local.ToString("g", CultureInfo.CurrentCulture)]
                            : S["{0} is already full at {1}.", target.Name, local.ToString("g", CultureInfo.CurrentCulture)],
                        CalendarError.ConflictCode));

                    break;
                }
            }
        }

        return errors;
    }

    private bool Counts(CalendarOccurrence occurrence, Calendar target)
    {
        var calendarEvent = occurrence.Event;

        if (calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            return false;
        }

        if (string.Equals(calendarEvent.CalendarId, target.ItemId, StringComparison.Ordinal))
        {
            return calendarEvent.Transparency == CalendarTransparency.Busy &&
                (_typeManager.FindEventType(target.Type, calendarEvent.EventType)?.CountsTowardCapacity ?? true);
        }

        return calendarEvent.Attendees.Any(attendee => attendee.ResourceCalendarId == target.ItemId && attendee.Response != CalendarAttendeeResponse.Declined);
    }

    private static bool IsSelf(CalendarOccurrence occurrence, CalendarEvent calendarEvent)
    {
        var other = occurrence.Event;

        if (string.Equals(other.ItemId, calendarEvent.ItemId, StringComparison.Ordinal))
        {
            return true;
        }

        // Editing a series: its overrides are part of it.
        if (string.Equals(other.SeriesId, calendarEvent.ItemId, StringComparison.Ordinal))
        {
            return true;
        }

        // Changing one occurrence: the occurrence it replaces doesn't count against it.
        return calendarEvent.SeriesId is not null &&
            string.Equals(occurrence.SeriesId, calendarEvent.SeriesId, StringComparison.Ordinal) &&
            occurrence.OriginalStartUtc == calendarEvent.OriginalStartUtc;
    }

    private static int GetPeak(IReadOnlyList<CalendarOccurrence> occurrences, DateTime startUtc, DateTime endUtc)
    {
        var points = new List<(DateTime Time, int Delta)>();

        foreach (var occurrence in occurrences)
        {
            if (occurrence.StartUtc < endUtc && occurrence.EndUtc > startUtc)
            {
                points.Add((occurrence.StartUtc < startUtc ? startUtc : occurrence.StartUtc, 1));
                points.Add((occurrence.EndUtc > endUtc ? endUtc : occurrence.EndUtc, -1));
            }
        }

        var current = 0;
        var peak = 0;

        // Ends sort before starts at the same moment, so back-to-back events don't overlap.
        foreach (var point in points.OrderBy(point => point.Time).ThenBy(point => point.Delta))
        {
            current += point.Delta;
            peak = Math.Max(peak, current);
        }

        return peak;
    }
}
