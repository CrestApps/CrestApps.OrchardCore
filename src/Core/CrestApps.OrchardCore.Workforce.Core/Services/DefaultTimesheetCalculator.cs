using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimesheetCalculator"/>. Times are kept to the second and lines are rounded to hundredths
/// of an hour at the end.
/// </summary>
internal sealed class DefaultTimesheetCalculator : ITimesheetCalculator
{
    // How long after a shift ends, or after clocking in when there is no shift, an open punch counts as missed.
    private static readonly TimeSpan _missedAfterShift = TimeSpan.FromHours(4);
    private static readonly TimeSpan _missedWithoutShift = TimeSpan.FromHours(16);

    // How far from a shift's start a stretch with no overlap still belongs to it.
    private static readonly TimeSpan _shiftReach = TimeSpan.FromHours(1);

    public TimesheetCalculation Calculate(TimesheetCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var policy = input.Policy ?? new TimeClockPolicy();
        var zone = string.IsNullOrEmpty(input.TimeZoneId) ? "UTC" : input.TimeZoneId;
        var codes = new PayCodes(input.PayCodes ?? []);
        var shifts = (input.Shifts ?? []).OrderBy(shift => shift.StartUtc).ToArray();
        var timeOffDays = (input.TimeOff ?? []).Select(day => day.Date).ToHashSet();
        var result = new TimesheetCalculation { TimeZoneId = zone };
        var exceptions = new Dictionary<string, AttendanceException>(StringComparer.Ordinal);

        bool InPeriod(DateOnly date) => date >= input.PeriodStart && date <= input.PeriodEnd;
        DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(CalendarTime.ToLocal(utc, zone));

        void Raise(AttendanceExceptionKind kind, DateOnly date, int minutes, string shiftKey, string punchId)
        {
            if (!InPeriod(date))
            {
                return;
            }

            var key = $"{kind}:{date:yyyyMMdd}:{shiftKey ?? punchId}";

            exceptions.TryAdd(key, new AttendanceException
            {
                Kind = kind,
                Date = date,
                Minutes = minutes,
                ShiftKey = shiftKey,
                PunchId = punchId,
                Key = key,
                Points = policy.PointsFor(kind),
            });
        }

        var stretches = Pair(input.Punches ?? []);

        foreach (var stretch in stretches)
        {
            stretch.Shift = FindShift(shifts, stretch.Start, stretch.Spans.Count > 0 ? stretch.Spans[^1].End ?? stretch.Start : stretch.Start);
        }

        foreach (var stretch in stretches.Where(stretch => stretch.IsOpen))
        {
            var open = stretch.Spans[^1];
            var missed = stretch.Shift is not null
                ? input.NowUtc > stretch.Shift.EndUtc + _missedAfterShift
                : input.NowUtc > stretch.Start + _missedWithoutShift;

            if (!missed)
            {
                stretch.Spans.Remove(open);
                result.OnTheClockSinceUtc = stretch.Start;

                continue;
            }

            Raise(AttendanceExceptionKind.MissedPunch, LocalDate(stretch.Start), 0, stretch.Shift?.Key, open.StartPunchId);

            if (policy.MissedPunchHandling == MissedPunchHandling.CloseAtScheduledEnd && stretch.Shift is not null && stretch.Shift.EndUtc > open.Start)
            {
                open.End = stretch.Shift.EndUtc;
                open.ClosedAtShiftEnd = true;
            }
            else
            {
                stretch.Spans.Remove(open);
            }
        }

        foreach (var stretch in stretches.Where(stretch => stretch.Spans.Count > 0))
        {
            Adjust(stretch, policy, shifts, Raise, LocalDate);
        }

        var spans = stretches.SelectMany(stretch => stretch.Spans).Where(span => span.End > span.Start).ToList();

        foreach (var span in spans.Where(span => span.Kind == TimesheetSpanKind.Meal && policy.LongMealMinutes is > 0))
        {
            var over = (int)Math.Round((span.End.Value - span.Start).TotalMinutes) - policy.LongMealMinutes.Value;

            if (over > 0)
            {
                Raise(AttendanceExceptionKind.LongMeal, LocalDate(span.Start), over, span.Stretch.Shift?.Key, span.EndPunchId ?? span.StartPunchId);
            }
        }

        foreach (var punch in input.Punches ?? [])
        {
            if (punch.WithinGeofence == false)
            {
                Raise(AttendanceExceptionKind.OutsideGeofence, LocalDate(punch.Utc), 0, null, punch.ItemId);
            }
        }

        var segments = Split(spans.Where(span => span.Kind != TimesheetSpanKind.Meal), zone);

        TakeAutoMeals(segments, spans, policy, LocalDate);

        var lines = new Dictionary<(DateOnly, string, string, string, TimesheetLineSource), LineTotal>();

        void Add(DateOnly date, string payCodeId, string activity, string job, TimesheetLineSource source, long seconds, IEnumerable<string> punchIds, string sourceId = null)
        {
            if (!InPeriod(date) || seconds <= 0)
            {
                return;
            }

            var key = (date, payCodeId, activity, job, source);

            if (!lines.TryGetValue(key, out var total))
            {
                lines[key] = total = new LineTotal();
            }

            total.Seconds += seconds;

            foreach (var id in punchIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                total.PunchIds.Add(id);
            }

            if (!string.IsNullOrEmpty(sourceId))
            {
                total.SourceIds.Add(sourceId);
            }
        }

        Classify(segments, policy, input.WeekStart, codes, Add);

        foreach (var differential in codes.Differentials)
        {
            foreach (var segment in segments)
            {
                var overlap = DifferentialSeconds(differential, segment, zone);

                Add(segment.Date, differential.ItemId, segment.ActivityCodeId, segment.Job, TimesheetLineSource.Punches, overlap, segment.PunchIds);
            }
        }

        foreach (var day in input.TimeOff ?? [])
        {
            Add(day.Date, string.IsNullOrEmpty(day.PayCodeId) ? codes.Of(PayCodeKind.PaidTimeOff) : day.PayCodeId, null, null, TimesheetLineSource.TimeOff, (long)(day.Hours * 3600), [], day.RequestId);
        }

        foreach (var entry in input.PredictabilityPay ?? [])
        {
            Add(LocalDate(entry.ShiftStartUtc), codes.Of(PayCodeKind.PredictabilityPay), null, null, TimesheetLineSource.PredictabilityPay, (long)(entry.Hours * 3600), [], entry.ItemId);
        }

        foreach (var (key, total) in lines.OrderBy(pair => pair.Key.Item1))
        {
            result.Lines.Add(new TimesheetLine
            {
                Date = key.Item1,
                PayCodeId = key.Item2,
                ActivityCodeId = key.Item3,
                Job = key.Item4,
                Source = key.Item5,
                Hours = Math.Round(total.Seconds / 3600m, 2),
                PunchIds = [.. total.PunchIds],
                SourceIds = [.. total.SourceIds],
            });
        }

        foreach (var line in input.ManualLines ?? [])
        {
            if (InPeriod(line.Date))
            {
                result.Lines.Add(line);
            }
        }

        foreach (var shift in shifts.Where(shift => shift.EndUtc <= input.NowUtc))
        {
            var date = LocalDate(shift.StartUtc);
            var worked = stretches.Any(stretch => stretch.Start < shift.EndUtc && (stretch.Spans.Count == 0 || (stretch.Spans[^1].End ?? input.NowUtc) > shift.StartUtc - _shiftReach));

            if (!worked && !timeOffDays.Contains(date))
            {
                Raise(AttendanceExceptionKind.NoShow, date, (int)Math.Round((shift.EndUtc - shift.StartUtc).TotalMinutes), shift.Key, null);
            }
        }

        result.WorkedMinutes = (int)Math.Round(segments.Where(segment => InPeriod(segment.Date)).Sum(segment => segment.Seconds) / 60d);
        result.Exceptions.AddRange(exceptions.Values.OrderBy(exception => exception.Date).ThenBy(exception => exception.Kind));

        foreach (var span in spans.Where(span => InPeriod(LocalDate(span.Start)) || InPeriod(LocalDate(span.End.Value))))
        {
            result.Spans.Add(new TimesheetSpan
            {
                Kind = span.Kind,
                StartUtc = span.Start,
                EndUtc = span.End.Value,
                ActivityCodeId = span.ActivityCodeId,
                Job = span.Job,
                StartPunchId = span.StartPunchId,
                EndPunchId = span.ClosedAtShiftEnd ? null : span.EndPunchId,
            });
        }

        return result;
    }

    /// <summary>
    /// Pairs punches into stretches of spans, each stretch from clocking in to clocking out.
    /// </summary>
    private static List<Stretch> Pair(IEnumerable<TimePunch> punches)
    {
        var stretches = new List<Stretch>();
        Stretch current = null;
        string activity = null;
        string job = null;

        void Open(TimesheetSpanKind kind, TimePunch punch)
        {
            current.Spans.Add(new Span
            {
                Kind = kind,
                Start = punch.Utc,
                StartPunchId = punch.ItemId,
                ActivityCodeId = activity,
                Job = job,
                Stretch = current,
            });
        }

        void Close(TimePunch punch)
        {
            var span = current.Spans[^1];

            span.End = punch.Utc;
            span.EndPunchId = punch.ItemId;
        }

        foreach (var punch in punches.OrderBy(punch => punch.Utc).ThenBy(punch => punch.CreatedUtc))
        {
            var state = current is null ? (TimesheetSpanKind?)null : current.Spans[^1].Kind;

            switch (punch.Kind)
            {
                case TimePunchKind.In:
                case TimePunchKind.JobStart when state is null:
                    // Clocking in long after an open stretch began starts a new one; the open one is a missed punch.
                    if (state is null || (current.IsOpen && punch.Utc - current.Start > _missedWithoutShift))
                    {
                        activity = punch.ActivityCodeId;
                        job = punch.Job;
                        current = new Stretch { Start = punch.Utc };
                        stretches.Add(current);
                        Open(TimesheetSpanKind.Work, punch);
                    }
                    else if (state != TimesheetSpanKind.Work)
                    {
                        Close(punch);
                        Open(TimesheetSpanKind.Work, punch);
                    }

                    break;

                case TimePunchKind.Out when state is not null:
                    Close(punch);
                    current = null;
                    break;

                case TimePunchKind.MealStart when state is TimesheetSpanKind.Work or TimesheetSpanKind.Break:
                    Close(punch);
                    Open(TimesheetSpanKind.Meal, punch);
                    break;

                case TimePunchKind.BreakStart when state == TimesheetSpanKind.Work:
                    Close(punch);
                    Open(TimesheetSpanKind.Break, punch);
                    break;

                case TimePunchKind.MealEnd when state == TimesheetSpanKind.Meal:
                case TimePunchKind.BreakEnd when state == TimesheetSpanKind.Break:
                    Close(punch);
                    Open(TimesheetSpanKind.Work, punch);
                    break;

                case TimePunchKind.ActivityChange when state == TimesheetSpanKind.Work:
                    activity = punch.ActivityCodeId;
                    Close(punch);
                    Open(TimesheetSpanKind.Work, punch);
                    break;

                case TimePunchKind.JobStart when state == TimesheetSpanKind.Work:
                    activity = punch.ActivityCodeId ?? activity;
                    job = punch.Job;
                    Close(punch);
                    Open(TimesheetSpanKind.Work, punch);
                    break;

                case TimePunchKind.JobEnd when state == TimesheetSpanKind.Work:
                    job = null;
                    Close(punch);
                    Open(TimesheetSpanKind.Work, punch);
                    break;
            }
        }

        return stretches;
    }

    private static TimesheetShift FindShift(IReadOnlyList<TimesheetShift> shifts, DateTime start, DateTime end)
    {
        if (end <= start)
        {
            end = start.AddMinutes(1);
        }

        return shifts
            .Select(shift => (Shift: shift, Overlap: (Min(end, shift.EndUtc) - Max(start, shift.StartUtc)).Ticks))
            .Where(pair => pair.Overlap > 0 || (start - pair.Shift.StartUtc).Duration() <= _shiftReach)
            .OrderByDescending(pair => pair.Overlap)
            .Select(pair => pair.Shift)
            .FirstOrDefault();
    }

    /// <summary>
    /// Applies grace against the stretch's shift and rounds the rest of its boundaries, raising late starts and
    /// early ends past the grace.
    /// </summary>
    private static void Adjust(Stretch stretch, TimeClockPolicy policy, IReadOnlyList<TimesheetShift> shifts, Action<AttendanceExceptionKind, DateOnly, int, string, string> raise, Func<DateTime, DateOnly> localDate)
    {
        var spans = stretch.Spans;
        var first = spans[0];
        var last = spans[^1];
        var shift = stretch.Shift;
        var startSnapped = false;
        var endSnapped = false;

        if (shift is not null)
        {
            var late = first.Start - shift.StartUtc;

            if ((late < TimeSpan.Zero && -late <= TimeSpan.FromMinutes(policy.EarlyGraceMinutes))
                || (late > TimeSpan.Zero && late <= TimeSpan.FromMinutes(policy.LateGraceMinutes)))
            {
                first.Start = shift.StartUtc;
                startSnapped = true;
            }
            else if (late > TimeSpan.FromMinutes(policy.LateGraceMinutes))
            {
                raise(AttendanceExceptionKind.Late, localDate(shift.StartUtc), (int)Math.Round(late.TotalMinutes), shift.Key, first.StartPunchId);
            }

            if (last.ClosedAtShiftEnd)
            {
                endSnapped = true;
            }
            else
            {
                var early = shift.EndUtc - last.End.Value;

                if ((early > TimeSpan.Zero && early <= TimeSpan.FromMinutes(policy.LateGraceMinutes))
                    || (early < TimeSpan.Zero && -early <= TimeSpan.FromMinutes(policy.EarlyGraceMinutes)))
                {
                    last.End = shift.EndUtc;
                    endSnapped = true;
                }
                else if (early > TimeSpan.FromMinutes(policy.LateGraceMinutes))
                {
                    raise(AttendanceExceptionKind.EarlyOut, localDate(shift.StartUtc), (int)Math.Round(early.TotalMinutes), shift.Key, last.EndPunchId);
                }
            }
        }

        if (policy.RoundingMinutes <= 0)
        {
            return;
        }

        var step = TimeSpan.FromMinutes(policy.RoundingMinutes);
        var favorWorker = policy.RoundingDirection == TimeRoundingDirection.FavorWorker;

        for (var index = 0; index <= spans.Count; index++)
        {
            var before = index > 0 ? spans[index - 1] : null;
            var after = index < spans.Count ? spans[index] : null;

            if ((before is null && startSnapped) || (after is null && endSnapped) || (after is null && before.End is null))
            {
                continue;
            }

            var paidBefore = before is not null && before.Kind != TimesheetSpanKind.Meal;
            var paidAfter = after is not null && after.Kind != TimesheetSpanKind.Meal;
            var mode = !favorWorker || paidBefore == paidAfter ? 0 : paidAfter ? -1 : 1;
            var time = Round(after?.Start ?? before.End.Value, step, mode);

            if (before is not null)
            {
                before.End = time;
            }

            if (after is not null)
            {
                after.Start = time;
            }
        }
    }

    private static DateTime Round(DateTime value, TimeSpan step, int mode)
    {
        var steps = value.Ticks / (double)step.Ticks;
        var rounded = mode switch
        {
            < 0 => Math.Floor(steps),
            > 0 => Math.Ceiling(steps),
            _ => Math.Floor(steps + 0.5),
        };

        return new DateTime((long)rounded * step.Ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Splits paid spans at midnight in the worker's time.
    /// </summary>
    private static List<Segment> Split(IEnumerable<Span> spans, string zone)
    {
        var segments = new List<Segment>();

        foreach (var span in spans.OrderBy(span => span.Start))
        {
            var start = span.Start;

            while (start < span.End.Value)
            {
                var date = DateOnly.FromDateTime(CalendarTime.ToLocal(start, zone));
                var midnight = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
                var end = Min(span.End.Value, midnight);

                if (end <= start)
                {
                    end = span.End.Value;
                }

                segments.Add(new Segment
                {
                    Date = date,
                    Start = start,
                    End = end,
                    ActivityCodeId = span.ActivityCodeId,
                    Job = span.Job,
                    PunchIds = [span.StartPunchId, span.EndPunchId],
                });

                start = end;
            }
        }

        return segments;
    }

    /// <summary>
    /// Takes the policy's meal off the end of a long day with no meal punched.
    /// </summary>
    private static void TakeAutoMeals(List<Segment> segments, IReadOnlyList<Span> spans, TimeClockPolicy policy, Func<DateTime, DateOnly> localDate)
    {
        if (policy.AutoMealAfterHours is not > 0 || policy.AutoMealMinutes <= 0)
        {
            return;
        }

        var mealDays = spans.Where(span => span.Kind == TimesheetSpanKind.Meal).Select(span => localDate(span.Start)).ToHashSet();

        foreach (var day in segments.GroupBy(segment => segment.Date).ToArray())
        {
            if (mealDays.Contains(day.Key) || day.Sum(segment => segment.Seconds) <= (long)(policy.AutoMealAfterHours.Value * 3600))
            {
                continue;
            }

            var remaining = TimeSpan.FromMinutes(policy.AutoMealMinutes);

            foreach (var segment in day.OrderByDescending(segment => segment.End))
            {
                var take = Min(remaining, segment.End - segment.Start);

                segment.End -= take;
                remaining -= take;

                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }
            }
        }

        segments.RemoveAll(segment => segment.End <= segment.Start);
    }

    /// <summary>
    /// Classifies paid time into regular, overtime and double time, in order, counting the day and the week.
    /// </summary>
    private static void Classify(IReadOnlyList<Segment> segments, TimeClockPolicy policy, DayOfWeek weekStart, PayCodes codes, Action<DateOnly, string, string, string, TimesheetLineSource, long, IEnumerable<string>, string> add)
    {
        var days = new Dictionary<DateOnly, long>();
        var weeks = new Dictionary<DateOnly, long>();
        var dailyOvertime = Seconds(policy.DailyOvertimeHours);
        var dailyDoubleTime = Seconds(policy.DailyDoubleTimeHours);
        var weeklyOvertime = Seconds(policy.WeeklyOvertimeHours);

        foreach (var segment in segments.OrderBy(segment => segment.Start))
        {
            var week = segment.Date.AddDays(-(((int)segment.Date.DayOfWeek - (int)weekStart + 7) % 7));
            var remaining = segment.Seconds;

            while (remaining > 0)
            {
                var day = days.GetValueOrDefault(segment.Date);
                var weekUsed = weeks.GetValueOrDefault(week);
                PayCodeKind kind;
                long boundary;

                if (dailyDoubleTime.HasValue && day >= dailyDoubleTime)
                {
                    kind = PayCodeKind.DoubleTime;
                    boundary = remaining;
                }
                else if ((dailyOvertime.HasValue && day >= dailyOvertime) || (weeklyOvertime.HasValue && weekUsed >= weeklyOvertime))
                {
                    kind = PayCodeKind.Overtime;
                    boundary = dailyDoubleTime.HasValue ? dailyDoubleTime.Value - day : remaining;
                }
                else
                {
                    kind = PayCodeKind.Regular;
                    boundary = new[]
                    {
                        dailyOvertime - day,
                        dailyDoubleTime - day,
                        weeklyOvertime - weekUsed,
                    }.Where(value => value.HasValue).Select(value => value.Value).DefaultIfEmpty(remaining).Min();
                }

                var take = Math.Min(remaining, Math.Max(1, boundary));

                add(segment.Date, codes.Of(kind), segment.ActivityCodeId, segment.Job, TimesheetLineSource.Punches, take, segment.PunchIds, null);
                days[segment.Date] = day + take;
                weeks[week] = weekUsed + take;
                remaining -= take;
            }
        }
    }

    private static long DifferentialSeconds(PayCode differential, Segment segment, string zone)
    {
        long total = 0;

        // A window belongs to the day it starts; one past midnight can reach this day from the day before.
        for (var day = segment.Date.AddDays(-1); day <= segment.Date; day = day.AddDays(1))
        {
            if (differential.Weekdays.Count > 0 && !differential.Weekdays.Contains(day.DayOfWeek))
            {
                continue;
            }

            var start = CalendarTime.ToUtc(day.ToDateTime(differential.WindowStart.Value), zone);
            var endDay = differential.WindowEnd.Value <= differential.WindowStart.Value ? day.AddDays(1) : day;
            var end = CalendarTime.ToUtc(endDay.ToDateTime(differential.WindowEnd.Value), zone);
            var overlap = Min(end, segment.End) - Max(start, segment.Start);

            if (overlap > TimeSpan.Zero)
            {
                total += (long)overlap.TotalSeconds;
            }
        }

        return total;
    }

    private static long? Seconds(decimal? hours)
    {
        return hours is > 0 ? (long)(hours.Value * 3600) : null;
    }

    private static DateTime Min(DateTime first, DateTime second) => first < second ? first : second;

    private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;

    private sealed class Stretch
    {
        public DateTime Start { get; set; }

        public List<Span> Spans { get; } = [];

        public TimesheetShift Shift { get; set; }

        public bool IsOpen => Spans.Count > 0 && Spans[^1].End is null;
    }

    private sealed class Span
    {
        public TimesheetSpanKind Kind { get; set; }

        public DateTime Start { get; set; }

        public DateTime? End { get; set; }

        public string StartPunchId { get; set; }

        public string EndPunchId { get; set; }

        public string ActivityCodeId { get; set; }

        public string Job { get; set; }

        public bool ClosedAtShiftEnd { get; set; }

        public Stretch Stretch { get; set; }
    }

    private sealed class Segment
    {
        public DateOnly Date { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public string ActivityCodeId { get; set; }

        public string Job { get; set; }

        public string[] PunchIds { get; set; }

        public long Seconds => (long)(End - Start).TotalSeconds;
    }

    private sealed class LineTotal
    {
        public long Seconds { get; set; }

        public SortedSet<string> PunchIds { get; } = new(StringComparer.Ordinal);

        public SortedSet<string> SourceIds { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// The pay codes in use, by kind.
    /// </summary>
    private sealed class PayCodes
    {
        private readonly Dictionary<PayCodeKind, string> _byKind = [];

        public PayCodes(IEnumerable<PayCode> codes)
        {
            foreach (var code in codes.Where(code => code.Enabled))
            {
                if (code.Kind == PayCodeKind.ShiftDifferential)
                {
                    if (code.WindowStart.HasValue && code.WindowEnd.HasValue)
                    {
                        Differentials.Add(code);
                    }
                }
                else
                {
                    _byKind.TryAdd(code.Kind, code.ItemId);
                }
            }
        }

        public List<PayCode> Differentials { get; } = [];

        /// <summary>
        /// Returns the code of a kind, falling back to regular for overtime and double time without their own.
        /// </summary>
        public string Of(PayCodeKind kind)
        {
            if (_byKind.TryGetValue(kind, out var id))
            {
                return id;
            }

            return kind switch
            {
                PayCodeKind.DoubleTime => Of(PayCodeKind.Overtime),
                PayCodeKind.Overtime or PayCodeKind.PaidTimeOff or PayCodeKind.PredictabilityPay => _byKind.GetValueOrDefault(PayCodeKind.Regular),
                _ => null,
            };
        }
    }
}
