namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Who can see an event's details.
/// </summary>
public enum CalendarEventVisibility
{
    /// <summary>
    /// The calendar's visibility decides.
    /// </summary>
    Default,

    /// <summary>
    /// Anyone who can see the calendar's free/busy time can also see the details.
    /// </summary>
    Public,

    /// <summary>
    /// Only people who can change the calendar can see the details; others see busy time.
    /// </summary>
    Private,
}
