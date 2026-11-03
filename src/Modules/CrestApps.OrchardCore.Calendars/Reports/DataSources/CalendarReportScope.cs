using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// The calendars a report's principal may read, and what it may do with each.
/// </summary>
internal sealed class CalendarReportScope
{
    private readonly Dictionary<string, CalendarAccess> _calendars;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReportScope"/> class.
    /// </summary>
    /// <param name="everyCalendar">Whether the principal may read every calendar.</param>
    /// <param name="calendars">The calendars the principal may read, with its access level on each.</param>
    public CalendarReportScope(
        bool everyCalendar,
        IEnumerable<CalendarAccess> calendars)
    {
        EveryCalendar = everyCalendar;
        _calendars = calendars.ToDictionary(access => access.Calendar.ItemId, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a value indicating whether the principal may read every calendar, so the read is not narrowed by calendar.
    /// </summary>
    public bool EveryCalendar { get; }

    /// <summary>
    /// Gets the ids of the calendars the principal may read, known so far.
    /// </summary>
    public IReadOnlyCollection<string> CalendarIds => _calendars.Keys;

    /// <summary>
    /// Gets what the principal may do with a calendar, when it is known.
    /// </summary>
    /// <param name="calendarId">The calendar's id.</param>
    /// <param name="access">The calendar with the principal's level.</param>
    /// <returns><see langword="true"/> when the calendar is known.</returns>
    public bool TryGet(string calendarId, out CalendarAccess access)
    {
        return _calendars.TryGetValue(calendarId ?? string.Empty, out access);
    }

    /// <summary>
    /// Remembers what the principal may do with a calendar that was looked up while reading.
    /// </summary>
    /// <param name="access">The calendar with the principal's level.</param>
    public void Add(CalendarAccess access)
    {
        _calendars[access.Calendar.ItemId] = access;
    }

    /// <summary>
    /// Gets a value indicating whether the details of an event, such as its title, must be hidden from the principal:
    /// a private event shows only as busy time to someone who can't change its calendar, as it does on the calendar.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="level">The principal's level on the event's calendar.</param>
    /// <returns><see langword="true"/> when the details must be hidden.</returns>
    public static bool HidesDetails(CalendarEvent calendarEvent, CalendarAccessLevel level)
    {
        return level < CalendarAccessLevel.Read ||
            (calendarEvent.Visibility == CalendarEventVisibility.Private && level < CalendarAccessLevel.Write);
    }
}
