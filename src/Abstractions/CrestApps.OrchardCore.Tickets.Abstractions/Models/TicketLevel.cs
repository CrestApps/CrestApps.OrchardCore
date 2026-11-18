namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A ticket's impact (how many people or how much of the business it touches) or urgency (how soon it must be dealt
/// with). With both set, the desk's priority matrix can give the ticket its priority.
/// </summary>
public enum TicketLevel
{
    /// <summary>
    /// Low.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High.
    /// </summary>
    High = 3,
}
