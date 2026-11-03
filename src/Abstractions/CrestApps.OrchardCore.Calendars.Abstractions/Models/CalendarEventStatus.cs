namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// The state of an event.
/// </summary>
public enum CalendarEventStatus
{
    /// <summary>
    /// The event is happening.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The event is not confirmed yet, for example time off waiting for approval or a held booking.
    /// </summary>
    Tentative,

    /// <summary>
    /// The event was cancelled. It stays on record but no longer takes time.
    /// </summary>
    Cancelled,
}
