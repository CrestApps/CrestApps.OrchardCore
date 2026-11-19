namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What a ticket status means, whatever a desk calls it: the life cycle, service levels and reports read the category.
/// </summary>
public enum TicketStatusCategory
{
    /// <summary>
    /// Nobody has worked on it yet.
    /// </summary>
    New,

    /// <summary>
    /// An agent is working on it.
    /// </summary>
    Open,

    /// <summary>
    /// Waiting for the requester.
    /// </summary>
    Pending,

    /// <summary>
    /// Waiting for someone else, such as a supplier.
    /// </summary>
    OnHold,

    /// <summary>
    /// Answered; reopens when the requester replies within the desk's reopen window.
    /// </summary>
    Solved,

    /// <summary>
    /// Finished and read-only; a reply starts a follow-up ticket.
    /// </summary>
    Closed,
}
