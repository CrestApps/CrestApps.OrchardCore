using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// The global calendar permissions. What a user can do with one calendar also depends on owning it, it being shared with them
/// and its visibility; see <c>ICalendarAccessService</c>.
/// </summary>
public static class CalendarPermissions
{
    /// <summary>
    /// Change, share and delete every calendar and its events.
    /// </summary>
    public static readonly Permission ManageAllCalendars = new("ManageAllCalendars", LocalizationSource.Create("Manage all calendars", typeof(CalendarPermissions)));

    /// <summary>
    /// See every calendar and its events.
    /// </summary>
    public static readonly Permission ViewAllCalendars = new("ViewAllCalendars", LocalizationSource.Create("View all calendars", typeof(CalendarPermissions)), [ManageAllCalendars]);

    /// <summary>
    /// Create calendars of the types people may create; the creator manages them.
    /// </summary>
    public static readonly Permission CreateCalendars = new("CreateCalendars", LocalizationSource.Create("Create calendars", typeof(CalendarPermissions)), [ManageAllCalendars]);

    /// <summary>
    /// Use the calendar: have a personal calendar and see the calendars one owns, is shared or that are visible to everyone.
    /// </summary>
    public static readonly Permission AccessCalendars = new("AccessCalendars", LocalizationSource.Create("Use calendars", typeof(CalendarPermissions)), [ViewAllCalendars, CreateCalendars]);

    /// <summary>
    /// See anyone's schedule, or everyone's: the events on the calendars a person owns and the events they attend. Check it
    /// with a <c>CalendarSchedule</c> resource; for one's own schedule <see cref="ListOwnCalendarEvents"/> is enough, which the
    /// calendar authorization handler takes care of.
    /// </summary>
    public static readonly Permission ListCalendarEvents = new("ListCalendarEvents", LocalizationSource.Create("List anyone's calendar events", typeof(CalendarPermissions)), [ViewAllCalendars]);

    /// <summary>
    /// See one's own schedule. Granted to everyone who uses calendars.
    /// </summary>
    public static readonly Permission ListOwnCalendarEvents = new("ListOwnCalendarEvents", LocalizationSource.Create("List own calendar events", typeof(CalendarPermissions)), [ListCalendarEvents, AccessCalendars]);

    /// <summary>
    /// See the details of anyone's event, such as its title, place and attendees, instead of a busy block. Check it with the
    /// <c>CalendarEvent</c> as the resource; the calendar authorization handler also grants it for one's own events (with
    /// <see cref="ViewOwnCalendarEvents"/>) and for events on calendars one can read.
    /// </summary>
    public static readonly Permission ViewCalendarEvents = new("ViewCalendarEvents", LocalizationSource.Create("View anyone's calendar events", typeof(CalendarPermissions)), [ViewAllCalendars]);

    /// <summary>
    /// See the details of one's own events: events one created, attends, or that are on one's own calendar. Granted to everyone
    /// who uses calendars.
    /// </summary>
    public static readonly Permission ViewOwnCalendarEvents = new("ViewOwnCalendarEvents", LocalizationSource.Create("View own calendar events", typeof(CalendarPermissions)), [ViewCalendarEvents, AccessCalendars]);
}
