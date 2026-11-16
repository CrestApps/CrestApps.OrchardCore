namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A column of a ticket board: the tickets in one or more statuses, such as a <em>Pending</em> column for
/// <em>Waiting for the requester</em> and <em>Waiting for a third party</em>.
/// </summary>
public sealed class TicketBoardColumn
{
    /// <summary>
    /// Gets or sets the column's id, unique on its board.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the column's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the statuses whose tickets the column shows. A status is in one column of a board at most.
    /// </summary>
    public List<string> StatusIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the status a card dropped on the column gets when the column has several; empty asks which one.
    /// </summary>
    public string DefaultStatusId { get; set; }

    /// <summary>
    /// Gets or sets how many tickets the column should hold at most; the header shows when it holds more. Empty sets
    /// no limit. It never refuses a move.
    /// </summary>
    public int? Limit { get; set; }
}
