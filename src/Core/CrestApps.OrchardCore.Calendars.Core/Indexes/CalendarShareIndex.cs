using CrestApps.OrchardCore.Calendars.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// One row per user or role a calendar is shared with, so the calendars shared with someone can be found without loading every
/// calendar.
/// </summary>
public sealed class CalendarShareIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the calendar's id.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets whether the principal is a user or a role.
    /// </summary>
    public CalendarPrincipalKind PrincipalKind { get; set; }

    /// <summary>
    /// Gets or sets the user id or role name.
    /// </summary>
    public string PrincipalId { get; set; }

    /// <summary>
    /// Gets or sets what the principal can do.
    /// </summary>
    public CalendarAccessLevel Access { get; set; }
}
