using CrestApps.Core.Models;
using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Pages through contacts.
/// </summary>
/// <remarks>
/// Separate from <see cref="IOmnichannelContactResolver"/> because the callers are different: a
/// resolver answers "who is this" during a live interaction, and this answers "who should we work
/// through" for a batch loader or a picker. A host can serve the first cheaply from an index while
/// the second needs a real query.
/// </remarks>
public interface IOmnichannelContactSearch
{
    /// <summary>
    /// Returns one page of the contacts matching a query.
    /// </summary>
    /// <param name="query">What to match and which page to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page, with the total count so a caller can size the work.</returns>
    Task<PageResult<OmnichannelContact>> SearchAsync(ContactSearchQuery query, CancellationToken cancellationToken = default);
}
