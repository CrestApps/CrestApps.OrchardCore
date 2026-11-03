namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// A user or role a calendar is shared with.
/// </summary>
public sealed class CalendarShare
{
    /// <summary>
    /// Gets or sets whether the principal is a user or a role.
    /// </summary>
    public CalendarPrincipalKind PrincipalKind { get; set; }

    /// <summary>
    /// Gets or sets the user id or the role name.
    /// </summary>
    public string PrincipalId { get; set; }

    /// <summary>
    /// Gets or sets the name shown for the principal, kept so the sharing list reads well without a lookup.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the principal can do with the calendar.
    /// </summary>
    public CalendarAccessLevel Access { get; set; } = CalendarAccessLevel.Read;
}
