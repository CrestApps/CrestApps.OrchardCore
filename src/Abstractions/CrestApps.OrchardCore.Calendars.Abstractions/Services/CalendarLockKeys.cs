namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The names of the distributed locks calendar bookings run under. The calendar event manager takes the lock of each
/// calendar whose overlap limit an event counts toward before it checks the limit; a module that books a calendar from
/// its own availability takes the same lock, so the two can never fill one slot twice.
/// </summary>
public static class CalendarLockKeys
{
    /// <summary>
    /// Gets the name of the lock that bookings of a calendar run under.
    /// </summary>
    /// <param name="calendarId">The calendar id.</param>
    /// <returns>The lock name.</returns>
    public static string Calendar(string calendarId)
    {
        ArgumentException.ThrowIfNullOrEmpty(calendarId);

        return "CALENDAR_" + calendarId;
    }
}
