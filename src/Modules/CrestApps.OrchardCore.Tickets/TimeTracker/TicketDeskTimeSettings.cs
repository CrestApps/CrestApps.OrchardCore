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

    /// <summary>
    /// Gets or sets the stage that work not billed to the customer is recorded in, or <see langword="null"/> when all
    /// the desk's ticket time is recorded in <see cref="StageId"/>. The categories of each stage decide what the time
    /// counts toward.
    /// </summary>
    public string NotBilledStageId { get; set; }
}
