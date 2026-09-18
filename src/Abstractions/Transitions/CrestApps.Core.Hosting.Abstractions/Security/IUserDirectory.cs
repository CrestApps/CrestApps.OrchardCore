using System.Security.Claims;

namespace CrestApps.Core.Security;

/// <summary>
/// Reads the host's users.
/// </summary>
/// <remarks>
/// Read-only and projection-shaped, so the suite can name an agent without depending on the host's
/// identity types. Writing to a user goes through <see cref="IUserProfileStore"/>, which is a
/// separate concern with very different durability requirements.
/// </remarks>
public interface IUserDirectory
{
    /// <summary>
    /// Finds a user by identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<UserSummary> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by sign-in name.
    /// </summary>
    /// <param name="userName">The sign-in name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<UserSummary> FindByNameAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the user behind a principal.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user, or <see langword="null"/> when the principal names none.</returns>
    Task<UserSummary> FindByPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds several users at once.
    /// </summary>
    /// <remarks>
    /// A batch member, because the callers that need it are rendering a list and would otherwise
    /// issue one lookup per row.
    /// </remarks>
    /// <param name="userIds">The user identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The users that exist, in no particular order.</returns>
    Task<IReadOnlyCollection<UserSummary>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
}
