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
}
