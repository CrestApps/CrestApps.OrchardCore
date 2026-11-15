namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A card dropped on a column of a board.
/// </summary>
public sealed class TicketBoardMove
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the column it was dropped on.
    /// </summary>
    public string ColumnId { get; set; }

    /// <summary>
    /// Gets or sets the status chosen, when the column has several; empty takes the column's default or asks.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the status the board showed the ticket in. When the ticket's status changed since, the move is
    /// refused, so nobody undoes a change they never saw. Empty skips the check.
    /// </summary>
    public string ShownStatusId { get; set; }
}
