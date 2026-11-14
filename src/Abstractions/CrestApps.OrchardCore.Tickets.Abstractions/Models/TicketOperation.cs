namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Something a user does with a ticket, each with a permission per desk.
/// </summary>
public enum TicketOperation
{
    /// <summary>
    /// View every ticket of the desk.
    /// </summary>
    ViewAll,

    /// <summary>
    /// View the tickets of the user's teams.
    /// </summary>
    ViewTeam,

    /// <summary>
    /// View the tickets assigned to the user.
    /// </summary>
    ViewAssigned,

    /// <summary>
    /// Change a ticket's properties.
    /// </summary>
    Edit,

    /// <summary>
    /// Send a public reply.
    /// </summary>
    Reply,

    /// <summary>
    /// Add an internal note.
    /// </summary>
    Note,

    /// <summary>
    /// Assign a ticket.
    /// </summary>
    Assign,

    /// <summary>
    /// Merge a ticket into another.
    /// </summary>
    Merge,

    /// <summary>
    /// Delete a ticket.
    /// </summary>
    Delete,
}
