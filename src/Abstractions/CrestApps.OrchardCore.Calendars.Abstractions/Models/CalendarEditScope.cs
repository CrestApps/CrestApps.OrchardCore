namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// Which occurrences of a repeating event a change applies to.
/// </summary>
public enum CalendarEditScope
{
    /// <summary>
    /// Only the chosen occurrence.
    /// </summary>
    ThisOccurrence,

    /// <summary>
    /// The chosen occurrence and every one after it.
    /// </summary>
    ThisAndFollowing,

    /// <summary>
    /// The whole series.
    /// </summary>
    AllOccurrences,
}
