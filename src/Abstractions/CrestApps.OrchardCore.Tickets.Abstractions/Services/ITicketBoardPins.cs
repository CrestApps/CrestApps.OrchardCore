using System.Security.Claims;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// The ticket boards each user pinned to the admin menu: each shows under Service Desk for that user only, in the order
/// pinned, while they may still see it.
/// </summary>
public interface ITicketBoardPins
{
    /// <summary>
    /// Gets the boards a user pinned, in the order pinned.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The boards' ids; none for an anonymous user.</returns>
    Task<IReadOnlyList<string>> GetAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a board to the user's admin menu, at the end, or unpins it.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="boardId">The board.</param>
    /// <param name="pinned">Whether to pin it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the user's pins were saved.</returns>
    Task<bool> SetAsync(ClaimsPrincipal user, string boardId, bool pinned, CancellationToken cancellationToken = default);
}
