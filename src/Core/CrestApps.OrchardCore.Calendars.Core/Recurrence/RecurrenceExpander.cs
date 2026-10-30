using NodaTime;

namespace CrestApps.OrchardCore.Calendars.Core.Recurrence;

/// <summary>
/// Expands a <see cref="RecurrenceRule"/> into occurrence starts, in wall-clock time, in ascending order. The first occurrence is
/// always the event's own start, as RFC 5545 asks. Expansion is lazy: callers stop reading once they pass the end of their range.
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>
    /// The most periods (days, weeks, months or years) one expansion walks, so a rule that never matches can't loop forever.
    /// </summary>
    public const int MaxPeriods = 100_000;

    /// <summary>
    /// Gets the occurrence starts of a rule, in ascending order.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="start">The event's first start, as a wall-clock time.</param>
    /// <param name="zone">The event's time zone, used to compare against a UTC <c>UNTIL</c>.</param>
    /// <returns>The occurrence starts.</returns>
    public static IEnumerable<LocalDateTime> GetOccurrences(RecurrenceRule rule, LocalDateTime start, DateTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(zone);

        return Iterate(rule, start, zone);
    }

    private static IEnumerable<LocalDateTime> Iterate(RecurrenceRule rule, LocalDateTime start, DateTimeZone zone)
    {
        var time = start.TimeOfDay;
        var startDate = start.Date;
        var produced = 0;

        if (IsPastUntil(rule, start, zone))
        {
            yield break;
        }

        yield return start;
        produced++;

        if (rule.Count.HasValue && produced >= rule.Count.Value)
        {
            yield break;
        }

        for (var period = 0; period < MaxPeriods; period++)
        {
            var candidates = GetCandidates(rule, startDate, period);

            if (rule.BySetPosition.Count > 0)
            {
                candidates = ApplySetPositions(candidates, rule.BySetPosition);
            }

            foreach (var date in candidates)
            {
                if (date <= startDate)
                {
                    continue;
                }

                var occurrence = date + time;

                if (IsPastUntil(rule, occurrence, zone))
                {
                    yield break;
                }

                yield return occurrence;
                produced++;

                if (rule.Count.HasValue && produced >= rule.Count.Value)
                {
                    yield break;
                }
            }
        }
    }

    private static bool IsPastUntil(RecurrenceRule rule, LocalDateTime occurrence, DateTimeZone zone)
    {
        if (!rule.Until.HasValue)
        {
            return false;
        }

        var until = rule.Until.Value;

        if (rule.UntilIsDate)
        {
            return occurrence.Date > LocalDate.FromDateTime(until);
        }

        if (rule.UntilIsUtc)
        {
            var instant = occurrence.InZoneLeniently(zone).ToInstant();

            return instant > Instant.FromDateTimeUtc(DateTime.SpecifyKind(until, DateTimeKind.Utc));
        }

        return occurrence > LocalDateTime.FromDateTime(until);
    }

    private static List<LocalDate> GetCandidates(RecurrenceRule rule, LocalDate startDate, int period)
    {
        var step = period * rule.Interval;

        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily => GetDailyCandidates(rule, startDate.PlusDays(step)),
            RecurrenceFrequency.Weekly => GetWeeklyCandidates(rule, startDate, step),
            RecurrenceFrequency.Monthly => GetMonthlyCandidates(rule, startDate, step),
            _ => GetYearlyCandidates(rule, startDate, step),
        };
    }

    private static List<LocalDate> GetDailyCandidates(RecurrenceRule rule, LocalDate date)
    {
        if (rule.ByMonth.Count > 0 && !rule.ByMonth.Contains(date.Month))
        {
            return [];
        }

        if (rule.ByMonthDay.Count > 0 && !MatchesMonthDay(rule.ByMonthDay, date))
        {
            return [];
        }

        if (rule.ByDay.Count > 0 && !rule.ByDay.Any(day => day.Day == date.DayOfWeek))
        {
            return [];
        }

        return [date];
    }

    private static List<LocalDate> GetWeeklyCandidates(RecurrenceRule rule, LocalDate startDate, int weeks)
    {
        var weekStart = startDate.Previous(rule.WeekStart);

        if (startDate.DayOfWeek == rule.WeekStart)
        {
            weekStart = startDate;
        }

        weekStart = weekStart.PlusWeeks(weeks);

        IEnumerable<IsoDayOfWeek> days = rule.ByDay.Count > 0
            ? rule.ByDay.Select(day => day.Day)
            : [startDate.DayOfWeek];

        var result = new List<LocalDate>();

        foreach (var day in days.Distinct())
        {
            var offset = ((int)day - (int)rule.WeekStart + 7) % 7;
            var date = weekStart.PlusDays(offset);

            if (rule.ByMonth.Count > 0 && !rule.ByMonth.Contains(date.Month))
            {
                continue;
            }

            result.Add(date);
        }

        result.Sort();

        return result;
    }

    private static List<LocalDate> GetMonthlyCandidates(RecurrenceRule rule, LocalDate startDate, int months)
    {
        if ((startDate.Year * 12L) + months > 9998L * 12)
        {
            return [];
        }

        var month = new LocalDate(startDate.Year, startDate.Month, 1).PlusMonths(months);

        if (rule.ByMonth.Count > 0 && !rule.ByMonth.Contains(month.Month))
        {
            return [];
        }

        return GetDaysInMonth(rule, month.Year, month.Month, startDate.Day);
    }

    private static List<LocalDate> GetYearlyCandidates(RecurrenceRule rule, LocalDate startDate, int years)
    {
        var year = startDate.Year + years;

        if (year > 9998)
        {
            return [];
        }

        if (rule.ByMonth.Count == 0 && rule.ByDay.Count > 0 && rule.ByMonthDay.Count == 0)
        {
            return GetWeekdaysInRange(rule.ByDay, new LocalDate(year, 1, 1), new LocalDate(year, 12, 31));
        }

        IEnumerable<int> months = rule.ByMonth.Count > 0
            ? rule.ByMonth
            : rule.ByMonthDay.Count > 0
                ? Enumerable.Range(1, 12)
                : [startDate.Month];

        var result = new List<LocalDate>();

        foreach (var month in months.Distinct().Order())
        {
            result.AddRange(GetDaysInMonth(rule, year, month, startDate.Day));
        }

        return result;
    }

    private static List<LocalDate> GetDaysInMonth(RecurrenceRule rule, int year, int month, int defaultDay)
    {
        var daysInMonth = CalendarSystem.Iso.GetDaysInMonth(year, month);
        var result = new List<LocalDate>();

        if (rule.ByMonthDay.Count > 0)
        {
            foreach (var day in rule.ByMonthDay)
            {
                var resolved = day > 0 ? day : daysInMonth + day + 1;

                if (resolved < 1 || resolved > daysInMonth)
                {
                    continue;
                }

                var date = new LocalDate(year, month, resolved);

                if (rule.ByDay.Count > 0 && !rule.ByDay.Any(weekday => weekday.Day == date.DayOfWeek))
                {
                    continue;
                }

                result.Add(date);
            }
        }
        else if (rule.ByDay.Count > 0)
        {
            result.AddRange(GetWeekdaysInRange(rule.ByDay, new LocalDate(year, month, 1), new LocalDate(year, month, daysInMonth)));
        }
        else if (defaultDay <= daysInMonth)
        {
            result.Add(new LocalDate(year, month, defaultDay));
        }

        result = result.Distinct().ToList();
        result.Sort();

        return result;
    }

    private static List<LocalDate> GetWeekdaysInRange(IEnumerable<RecurrenceWeekday> weekdays, LocalDate first, LocalDate last)
    {
        var result = new List<LocalDate>();

        foreach (var weekday in weekdays)
        {
            var matches = new List<LocalDate>();
            var date = first.DayOfWeek == weekday.Day ? first : first.Next(weekday.Day);

            while (date <= last)
            {
                matches.Add(date);
                date = date.PlusWeeks(1);
            }

            if (weekday.Ordinal == 0)
            {
                result.AddRange(matches);
            }
            else
            {
                var index = weekday.Ordinal > 0 ? weekday.Ordinal - 1 : matches.Count + weekday.Ordinal;

                if (index >= 0 && index < matches.Count)
                {
                    result.Add(matches[index]);
                }
            }
        }

        result = result.Distinct().ToList();
        result.Sort();

        return result;
    }

    private static List<LocalDate> ApplySetPositions(List<LocalDate> candidates, IList<int> positions)
    {
        var result = new List<LocalDate>();

        foreach (var position in positions)
        {
            var index = position > 0 ? position - 1 : candidates.Count + position;

            if (index >= 0 && index < candidates.Count)
            {
                result.Add(candidates[index]);
            }
        }

        result = result.Distinct().ToList();
        result.Sort();

        return result;
    }

    private static bool MatchesMonthDay(IList<int> monthDays, LocalDate date)
    {
        var daysInMonth = CalendarSystem.Iso.GetDaysInMonth(date.Year, date.Month);

        return monthDays.Any(day => (day > 0 ? day : daysInMonth + day + 1) == date.Day);
    }
}
