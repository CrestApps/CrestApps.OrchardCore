using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A column of a board with its tickets, as a viewer sees it.
/// </summary>
public sealed class TicketBoardColumnTickets
{
    /// <summary>
    /// Gets the column.
    /// </summary>
    public TicketBoardColumn Column { get; init; }

    /// <summary>
    /// Gets the column's statuses that still exist, in the order of the status list.
    /// </summary>
    public IReadOnlyList<TicketStatus> Statuses { get; init; } = [];

    /// <summary>
    /// Gets the tickets shown, at most <see cref="Services.ITicketBoardService.CardsPerColumn"/>.
    /// </summary>
    public IReadOnlyList<ContentItem> Tickets { get; init; } = [];

    /// <summary>
    /// Gets how many tickets the column holds for the viewer, shown or not.
    /// </summary>
    public int Total { get; init; }
}
