namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Which copy wins when an event changed both here and in the external calendar since it was last synchronized.
/// </summary>
public enum CalendarSyncConflictPolicy
{
    /// <summary>
    /// The external calendar's copy wins: the local change is overwritten. The default, since the external calendar is usually
    /// where the event's owner works.
    /// </summary>
    ExternalWins,

    /// <summary>
    /// The local copy wins: it is written over the external copy, and an event deleted externally is created again.
    /// </summary>
    LocalWins,
}
