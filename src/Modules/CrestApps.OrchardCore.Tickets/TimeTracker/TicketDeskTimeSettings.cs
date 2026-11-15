namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// A desk's ticket time, kept in the desk's properties when Time Tracker is on.
/// </summary>
public sealed class TicketDeskTimeSettings
{
    /// <summary>
    /// Gets or sets the Time Tracker stage that work on the desk's tickets is recorded in, or <see langword="null"/>
    /// when the desk's tickets don't record time.
    /// </summary>
    public string StageId { get; set; }
}
