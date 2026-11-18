using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What a move on a board did: the ticket changed, stayed, needs a status chosen, or was refused and why.
/// </summary>
public sealed class TicketBoardMoveResult
{
    /// <summary>
    /// Gets the ticket, as it is now.
    /// </summary>
    public ContentItem Ticket { get; init; }

    /// <summary>
    /// Gets whether the ticket's status changed.
    /// </summary>
    public bool Changed { get; init; }

    /// <summary>
    /// Gets the statuses to choose from, when the column has several and no default.
    /// </summary>
    public IReadOnlyList<TicketStatus> Choices { get; init; } = [];

    /// <summary>
    /// Gets whether the ticket changed since the board was loaded, so the board should load again.
    /// </summary>
    public bool Stale { get; init; }

    /// <summary>
    /// Gets why the move was refused.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets whether the move was made, or left the ticket as it was without an error.
    /// </summary>
    public bool Succeeded => Errors.Count == 0 && Choices.Count == 0;

    /// <summary>
    /// Gets whether a status has to be chosen first.
    /// </summary>
    public bool NeedsChoice => Choices.Count > 0;
}
