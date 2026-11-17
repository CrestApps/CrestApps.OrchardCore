using System.Security.Claims;

namespace CrestApps.OrchardCore.SourceAccess;

/// <summary>
/// Decides who may see and act on the records of one source kind that other building blocks keep, such as the checks
/// and deposits that pay a <c>PayStatement</c>, on top of those blocks' own permissions. The module that writes the
/// records implements it once for its source kind, and every block that keeps such records (Checks, Disbursements)
/// applies it. Records of a source kind without one are visible to anyone the permissions allow. Register with
/// <see cref="SourceAccessServiceCollectionExtensions.AddSourceAccess{TAccess}"/>.
/// </summary>
public interface ISourceAccess
{
    /// <summary>
    /// Gets the source kind this access decides.
    /// </summary>
    string SourceKind { get; }

    /// <summary>
    /// Returns the records of the source kind a person may see or act on.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="level">What the person wants to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    Task<SourceAccessScope> GetScopeAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default);
}
