using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A board of tickets: cards in columns, each column holding one or more statuses. Dragging a card to another column
/// sets the ticket's status. A board shows only the tickets its viewer may see.
/// </summary>
public sealed class TicketBoard : CatalogItem, ITicketSharedRecord
{
    /// <summary>
    /// The most columns a board has.
    /// </summary>
    public const int MaxColumns = 20;

    /// <summary>
    /// Gets or sets the board's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the board is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the columns, from left to right.
    /// </summary>
    public List<TicketBoardColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets which tickets the board shows (desks, teams, ticket types, priorities, assignee) and the order of
    /// the cards; the columns decide the statuses, so the query's statuses and categories are not used.
    /// </summary>
    public TicketQuery Query { get; set; } = new();

    /// <summary>
    /// Gets or sets the user who keeps it; empty for a board everybody (or a team) shares.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the team it is shared with; empty shares it with everybody (or nobody, when it has an owner).
    /// </summary>
    public string SharedWithTeamId { get; set; }

    /// <summary>
    /// Gets or sets where it is listed; lower comes first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
