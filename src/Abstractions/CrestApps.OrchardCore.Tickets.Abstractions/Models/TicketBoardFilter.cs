namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What the viewer narrows a board to for a while, without changing the board.
/// </summary>
public sealed class TicketBoardFilter
{
    /// <summary>
    /// Gets or sets the text to find in the number or subject.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets whose tickets to show: <see cref="TicketAssignees.Me"/>, <see cref="TicketAssignees.Unassigned"/> or
    /// a user id; empty keeps the board's own.
    /// </summary>
    public string Assignee { get; set; }
}
