using System.Security.Claims;
using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Applies every <see cref="ICheckSourceAccess"/> in one place: the register page, the Check Register data set, the
/// register export and every action on a check by its id.
/// </summary>
public interface ICheckAccessService
{
    /// <summary>
    /// Returns the checks a person may see or act on.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="level">What the person wants to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filter.</returns>
    Task<CheckAccessFilter> GetFilterAsync(ClaimsPrincipal user, CheckAccessLevel level, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a person may see or act on a check.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="check">The check.</param>
    /// <param name="level">What the person wants to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the person may.</returns>
    Task<bool> CanAccessAsync(ClaimsPrincipal user, CheckRecord check, CheckAccessLevel level, CancellationToken cancellationToken = default);
}
