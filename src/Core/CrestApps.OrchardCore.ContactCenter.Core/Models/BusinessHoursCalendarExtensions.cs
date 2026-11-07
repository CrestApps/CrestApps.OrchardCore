using CrestApps.OrchardCore.BusinessHours.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Reads a stored <see cref="BusinessHoursCalendar"/> as the shared <see cref="HoursSchedule"/>.
/// </summary>
/// <remarks>
/// The calendar keeps its stored shape (and its document type, which YesSql records with the assembly name), so
/// existing calendars stay readable; the shared business hours engine evaluates the schedule it maps to.
/// </remarks>
public static class BusinessHoursCalendarExtensions
{
    /// <summary>
    /// Maps a calendar to a schedule: its open days become weekly windows and its holidays dates closed all day.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The schedule.</returns>
    public static HoursSchedule ToHoursSchedule(this BusinessHoursCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return new HoursSchedule
        {
            TimeZoneId = calendar.TimeZoneId,
            Windows = [.. (calendar.WeeklySchedule ?? [])
                .Where(day => day is not null && day.IsOpen)
                .Select(day => new HoursWindow { Day = day.Day, OpenMinute = day.OpenMinute, CloseMinute = day.CloseMinute })],
            DateExceptions = [.. (calendar.Holidays ?? [])
                .Distinct()
                .Select(date => new HoursDateException { Date = date, Closed = true })],
        };
    }
}
