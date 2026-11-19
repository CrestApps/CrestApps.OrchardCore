using System.Security.Claims;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Ticket boards as a user sees and works them: the boards they may open and arrange, the tickets of each column
/// (only those they may see), and moving a card, which sets the ticket's status through <see cref="ITicketService"/>.
/// </summary>
public interface ITicketBoardService
{
    /// <summary>
    /// The most cards a column shows.
    /// </summary>
    const int CardsPerColumn = 50;

    /// <summary>
    /// Lists the boards the user may open: shared with everybody, shared with one of their teams, or their own. A user
    /// who may view no desk's tickets gets none.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The boards, in their order and then by name.</returns>
    Task<IReadOnlyList<TicketBoard>> ListVisibleAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a board the user may open.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="boardId">The board.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The board, or <see langword="null"/> when there is none or the user may not open it.</returns>
    Task<TicketBoard> FindVisibleAsync(ClaimsPrincipal user, string boardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the user may create boards: their own, or shared ones.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>Whether they may.</returns>
    Task<bool> CanCreateAsync(ClaimsPrincipal user);

    /// <summary>
    /// Returns whether the user may share boards with everybody or a team.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>Whether they may.</returns>
    Task<bool> CanShareAsync(ClaimsPrincipal user);

    /// <summary>
    /// Returns whether the user may arrange or delete a board: a shared board needs
    /// <see cref="TicketsPermissions.ManageTicketBoards"/>, their own board
    /// <see cref="TicketsPermissions.ManageOwnTicketBoards"/>, and nobody arranges another user's personal board.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="board">The board.</param>
    /// <returns>Whether they may.</returns>
    Task<bool> CanManageAsync(ClaimsPrincipal user, TicketBoard board);

    /// <summary>
    /// Loads the tickets of each column, as the user may see them.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="board">The board.</param>
    /// <param name="filter">What the user narrows the board to, if anything.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The columns, from left to right.</returns>
    Task<IReadOnlyList<TicketBoardColumnTickets>> LoadAsync(ClaimsPrincipal user, TicketBoard board, TicketBoardFilter filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a card to a column: sets the ticket's status to the status chosen, the column's default or its only
    /// status, through <see cref="ITicketService.UpdateAsync"/>, which checks the life cycle and approval holds. The user
    /// needs to be able to edit the ticket.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="board">The board.</param>
    /// <param name="move">The move.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What the move did.</returns>
    Task<TicketBoardMoveResult> MoveAsync(ClaimsPrincipal user, TicketBoard board, TicketBoardMove move, CancellationToken cancellationToken = default);
}
