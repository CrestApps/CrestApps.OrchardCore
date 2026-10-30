using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A calendar and what a user can do with it.
/// </summary>
/// <param name="Calendar">The calendar.</param>
/// <param name="Level">The user's access level.</param>
public sealed record CalendarAccess(Calendar Calendar, CalendarAccessLevel Level);
