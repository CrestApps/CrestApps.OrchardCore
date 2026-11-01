using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out what each day of a request counts. A day with published shifts counts their paid time; a weekday with
/// none counts the type's day length; Saturday and Sunday with none count nothing. Part of a day counts the paid time
/// of the shifts inside it, or its length when there are none.
/// </summary>
internal static class TimeOffDayCounter
{
    public static IReadOnlyList<TimeOffRequestDay> Count(TimeOffType type, DateOnly start, DateOnly end, TimeOnly? startTime, TimeOnly? endTime, string timeZoneId, IReadOnlyList<CalendarEvent> shifts, IReadOnlyDictionary<string, ActivityCode> codes)
    {
        var days = new List<TimeOffRequestDay>();
        var partial = startTime.HasValue && endTime.HasValue;
        var dayMinutes = type.HoursPerDay * 60m;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var dayShifts = shifts
                .Where(shift => DateOnly.FromDateTime(CalendarTime.ToLocal(shift.StartUtc, timeZoneId)) == date)
                .Select(shift => (Event: shift, Read: ShiftEvents.TryRead(shift, out var data), Data: data))
                .Where(item => item.Read)
                .ToArray();
            int minutes;
            decimal amount;

            if (partial)
            {
                var fromUtc = CalendarTime.ToUtc(date.ToDateTime(startTime.Value), timeZoneId);
                var toUtc = CalendarTime.ToUtc(date.ToDateTime(endTime.Value), timeZoneId);

                minutes = dayShifts.Length > 0
                    ? dayShifts.Sum(item => ShiftEvents.PaidMinutesWithin(item.Event, item.Data, codes, fromUtc, toUtc))
                    : (int)Math.Max(0, (toUtc - fromUtc).TotalMinutes);
                amount = type.Unit == TimeOffUnit.Hours ? minutes / 60m : minutes / dayMinutes;
            }
            else if (dayShifts.Length > 0)
            {
                minutes = dayShifts.Sum(item => ShiftEvents.PaidMinutes(item.Event, item.Data, codes));
                amount = type.Unit == TimeOffUnit.Hours ? minutes / 60m : 1;
            }
            else if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                minutes = (int)dayMinutes;
                amount = type.Unit == TimeOffUnit.Hours ? type.HoursPerDay : 1;
            }
            else
            {
                minutes = 0;
                amount = 0;
            }

            days.Add(new TimeOffRequestDay
            {
                Date = date,
                Amount = Math.Round(amount, 2),
                Minutes = minutes,
            });
        }

        return days;
    }
}
