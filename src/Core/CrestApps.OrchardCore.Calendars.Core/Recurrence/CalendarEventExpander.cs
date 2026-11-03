using CrestApps.OrchardCore.Calendars.Models;
using NodaTime;

namespace CrestApps.OrchardCore.Calendars.Core.Recurrence;

/// <summary>
/// Works out an event's UTC times and expands a repeating event into occurrences.
/// </summary>
public static class CalendarEventExpander
{
    /// <summary>
    /// Sets an event's <see cref="CalendarEvent.StartUtc"/>, <see cref="CalendarEvent.EndUtc"/> and
    /// <see cref="CalendarRecurrence.EndUtc"/> from its wall-clock times, time zone and rule.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    public static void ComputeTimes(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var zone = CalendarTime.GetZone(calendarEvent.TimeZoneId);

        if (calendarEvent.AllDay)
        {
            calendarEvent.Start = calendarEvent.Start.Date;
            calendarEvent.End = calendarEvent.End.Date <= calendarEvent.Start ? calendarEvent.Start.AddDays(1) : calendarEvent.End.Date;
        }

        calendarEvent.StartUtc = CalendarTime.ToUtc(CalendarTime.ToLocalDateTime(calendarEvent.Start), zone);
        calendarEvent.EndUtc = CalendarTime.ToUtc(CalendarTime.ToLocalDateTime(calendarEvent.End), zone);

        if (calendarEvent.Recurrence is not null)
        {
            calendarEvent.Recurrence.EndUtc = ComputeSeriesEndUtc(calendarEvent);
        }
    }

    /// <summary>
    /// Gets when the last occurrence of a repeating event ends, in UTC, or <see langword="null"/> when it never ends.
    /// </summary>
    /// <param name="calendarEvent">The event, with its UTC times computed.</param>
    /// <returns>The end of the series.</returns>
    public static DateTime? ComputeSeriesEndUtc(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (!TryGetRule(calendarEvent, out var rule))
        {
            return calendarEvent.EndUtc;
        }

        if (!rule.IsFinite)
        {
            return null;
        }

        var zone = CalendarTime.GetZone(calendarEvent.TimeZoneId);
        LocalDateTime? last = null;

        foreach (var occurrence in RecurrenceExpander.GetOccurrences(rule, CalendarTime.ToLocalDateTime(calendarEvent.Start), zone))
        {
            last = occurrence;
        }

        return last.HasValue
            ? GetEndUtc(calendarEvent, last.Value, zone)
            : calendarEvent.EndUtc;
    }

    /// <summary>
    /// Gets the end of the range in which an event can have occurrences, in UTC: its end for a one-time event, the end of the
    /// series for a repeating one, and <see cref="DateTime.MaxValue"/> for a series that never ends.
    /// </summary>
    /// <param name="calendarEvent">The event, with its UTC times computed.</param>
    /// <returns>The end of the range.</returns>
    public static DateTime GetRangeEndUtc(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (calendarEvent.Recurrence is null || string.IsNullOrWhiteSpace(calendarEvent.Recurrence.Rule))
        {
            return calendarEvent.EndUtc;
        }

        return calendarEvent.Recurrence.EndUtc ?? DateTime.MaxValue;
    }

    /// <summary>
    /// Expands an event into the occurrences that overlap a range. Occurrences that were changed (whose original start is in
    /// <paramref name="overriddenStartsUtc"/>) and occurrences removed by an exception date are left out; the caller adds the
    /// changed ones from their own events.
    /// </summary>
    /// <param name="calendarEvent">The event or series.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range (exclusive).</param>
    /// <param name="overriddenStartsUtc">The original starts of occurrences that have their own event.</param>
    /// <param name="maxOccurrences">The most occurrences to return.</param>
    /// <returns>The occurrences, in ascending order.</returns>
    public static IEnumerable<CalendarOccurrence> Expand(
        CalendarEvent calendarEvent,
        DateTime fromUtc,
        DateTime toUtc,
        ISet<DateTime> overriddenStartsUtc = null,
        int maxOccurrences = 5000)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (!TryGetRule(calendarEvent, out var rule))
        {
            if (calendarEvent.StartUtc < toUtc && Overlaps(calendarEvent.StartUtc, calendarEvent.EndUtc, fromUtc))
            {
                yield return new CalendarOccurrence
                {
                    Event = calendarEvent,
                    SeriesId = calendarEvent.ItemId,
                    OriginalStartUtc = calendarEvent.StartUtc,
                    Start = calendarEvent.Start,
                    End = calendarEvent.End,
                    StartUtc = calendarEvent.StartUtc,
                    EndUtc = calendarEvent.EndUtc,
                };
            }

            yield break;
        }

        var zone = CalendarTime.GetZone(calendarEvent.TimeZoneId);
        var exceptions = new HashSet<LocalDateTime>(calendarEvent.Recurrence.ExceptionDates.Select(CalendarTime.ToLocalDateTime));
        var exceptionDates = new HashSet<LocalDate>(exceptions.Select(exception => exception.Date));
        var count = 0;

        foreach (var local in RecurrenceExpander.GetOccurrences(rule, CalendarTime.ToLocalDateTime(calendarEvent.Start), zone))
        {
            var startUtc = CalendarTime.ToUtc(local, zone);

            if (startUtc >= toUtc)
            {
                yield break;
            }

            if (calendarEvent.AllDay ? exceptionDates.Contains(local.Date) : exceptions.Contains(local))
            {
                continue;
            }

            if (overriddenStartsUtc is not null && overriddenStartsUtc.Contains(startUtc))
            {
                continue;
            }

            var endUtc = GetEndUtc(calendarEvent, local, zone);

            if (!Overlaps(startUtc, endUtc, fromUtc))
            {
                continue;
            }

            yield return new CalendarOccurrence
            {
                Event = calendarEvent,
                SeriesId = calendarEvent.ItemId,
                OriginalStartUtc = startUtc,
                Start = local.ToDateTimeUnspecified(),
                End = GetEndLocal(calendarEvent, local, endUtc, zone),
                StartUtc = startUtc,
                EndUtc = endUtc,
                IsRecurring = true,
            };

            if (++count >= maxOccurrences)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Gets whether a repeating event has an occurrence that was due to start at a given moment.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="originalStartUtc">The moment, in UTC.</param>
    /// <returns><see langword="true"/> when the rule makes an occurrence then.</returns>
    public static bool HasOccurrenceAt(CalendarEvent calendarEvent, DateTime originalStartUtc)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (!TryGetRule(calendarEvent, out var rule))
        {
            return calendarEvent.StartUtc == originalStartUtc;
        }

        var zone = CalendarTime.GetZone(calendarEvent.TimeZoneId);

        foreach (var local in RecurrenceExpander.GetOccurrences(rule, CalendarTime.ToLocalDateTime(calendarEvent.Start), zone))
        {
            var startUtc = CalendarTime.ToUtc(local, zone);

            if (startUtc == originalStartUtc)
            {
                return true;
            }

            if (startUtc > originalStartUtc)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets an event's recurrence rule.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when the event repeats with a valid rule.</returns>
    public static bool TryGetRule(CalendarEvent calendarEvent, out RecurrenceRule rule)
    {
        rule = null;

        return calendarEvent?.Recurrence is not null &&
            !string.IsNullOrWhiteSpace(calendarEvent.Recurrence.Rule) &&
            RecurrenceRule.TryParse(calendarEvent.Recurrence.Rule, out rule, out _);
    }

    private static DateTime GetEndUtc(CalendarEvent calendarEvent, LocalDateTime start, DateTimeZone zone)
    {
        if (calendarEvent.AllDay)
        {
            var days = Math.Max(1, (calendarEvent.End.Date - calendarEvent.Start.Date).Days);

            return CalendarTime.ToUtc(start.Date.PlusDays(days).AtMidnight(), zone);
        }

        return CalendarTime.ToUtc(start, zone) + (calendarEvent.EndUtc - calendarEvent.StartUtc);
    }

    private static DateTime GetEndLocal(CalendarEvent calendarEvent, LocalDateTime start, DateTime endUtc, DateTimeZone zone)
    {
        if (calendarEvent.AllDay)
        {
            var days = Math.Max(1, (calendarEvent.End.Date - calendarEvent.Start.Date).Days);

            return start.Date.PlusDays(days).AtMidnight().ToDateTimeUnspecified();
        }

        return Instant.FromDateTimeUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc)).InZone(zone).LocalDateTime.ToDateTimeUnspecified();
    }

    private static bool Overlaps(DateTime startUtc, DateTime endUtc, DateTime fromUtc)
    {
        // A zero-length event at the very start of the range still counts.
        return endUtc > fromUtc || (endUtc == startUtc && startUtc >= fromUtc);
    }
}
