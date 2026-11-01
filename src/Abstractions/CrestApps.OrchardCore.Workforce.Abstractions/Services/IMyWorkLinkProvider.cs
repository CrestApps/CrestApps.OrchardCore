using System.Security.Claims;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Adds links to the My Work portal's menu, so features in other modules can add pages to the portal.
/// </summary>
public interface IMyWorkLinkProvider
{
    /// <summary>
    /// Gets the links to show a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="worker">The user's worker, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The links.</returns>
    Task<IEnumerable<MyWorkLink>> GetLinksAsync(ClaimsPrincipal user, WorkerProfile worker, CancellationToken cancellationToken = default);
}

/// <summary>
/// A link in the My Work portal's menu.
/// </summary>
/// <param name="Key">The page's key, which marks the link active on its page.</param>
/// <param name="Title">The link's text.</param>
/// <param name="Path">The page's path under the site, such as <c>~/my-work/offers</c>.</param>
/// <param name="Badge">A count to show next to the link, or zero.</param>
public sealed record MyWorkLink(string Key, string Title, string Path, int Badge = 0);
