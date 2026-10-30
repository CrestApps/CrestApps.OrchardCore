namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Who can see a calendar besides its owner and the people it is shared with.
/// </summary>
public enum CalendarVisibility
{
    /// <summary>
    /// Only the owner, the people it is shared with and calendar administrators.
    /// </summary>
    Private,

    /// <summary>
    /// Every signed-in user can see when its events are busy, but not their details.
    /// </summary>
    Shared,

    /// <summary>
    /// Every signed-in user can see its events and their details.
    /// </summary>
    Public,
}
