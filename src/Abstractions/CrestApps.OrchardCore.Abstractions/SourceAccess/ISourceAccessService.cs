using System.Security.Claims;

namespace CrestApps.OrchardCore.SourceAccess;

/// <summary>
/// Applies every <see cref="ISourceAccess"/> in one place, for the lists, data sets, exports and actions of the
/// building blocks that keep records of other modules' sources.
/// </summary>
public interface ISourceAccessService
{
    /// <summary>
    /// Returns the records a person may see or act on. With no person (a run outside a request), every record a
    /// source restricts is left out.
    /// </summary>
    /// <param name="user">The person, or <see langword="null"/>.</param>
    /// <param name="level">What the person wants to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The filter.</returns>
    Task<SourceAccessFilter> GetFilterAsync(ClaimsPrincipal user, SourceAccessLevel level, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a person may see or act on a record.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="sourceKind">The record's source kind.</param>
    /// <param name="accessScope">The record's access scope.</param>
    /// <param name="level">What the person wants to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the person may.</returns>
    Task<bool> CanAccessAsync(ClaimsPrincipal user, string sourceKind, string accessScope, SourceAccessLevel level, CancellationToken cancellationToken = default);
}
