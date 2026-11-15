namespace CrestApps.OrchardCore.Tickets.ContactCenter.Models;

/// <summary>
/// The Contact Center queue a desk offers its new tickets through, in the desk's properties.
/// </summary>
public sealed class TicketDeskQueueSettings
{
    /// <summary>
    /// Gets or sets the queue, or <see langword="null"/> when the desk's tickets aren't offered through Contact Center.
    /// </summary>
    public string QueueId { get; set; }
}

/// <summary>
/// The ticket a Contact Center activity offers, in the activity's properties.
/// </summary>
public sealed class TicketActivityLink
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }
}
