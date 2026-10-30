namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// The kind of principal a calendar is shared with.
/// </summary>
public enum CalendarPrincipalKind
{
    /// <summary>
    /// A user, by user id.
    /// </summary>
    User,

    /// <summary>
    /// Everyone in a role, by role name.
    /// </summary>
    Role,
}
