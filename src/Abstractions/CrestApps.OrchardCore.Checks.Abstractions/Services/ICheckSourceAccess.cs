using System.Security.Claims;
using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Decides who may see and act on the checks of one source kind, such as paychecks, on top of the Checks permissions.
/// The module that writes the checks implements it; the checks of a source kind without one are visible to anyone the
/// permissions allow.
/// </summary>
public interface ICheckSourceAccess
{
    /// <summary>
    /// Gets the source kind (<see cref="CheckRecord.SourceKind"/>) this access decides.
    /// </summary>
    string SourceKind { get; }

    /// <summary>
    /// Returns the checks of the source kind a person may see or act on.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="level">What the person wants to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    Task<CheckSourceScope> GetScopeAsync(ClaimsPrincipal user, CheckAccessLevel level, CancellationToken cancellationToken = default);
}
