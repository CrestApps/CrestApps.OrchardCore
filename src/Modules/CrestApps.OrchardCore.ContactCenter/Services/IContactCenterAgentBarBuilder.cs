using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Builds the client configuration for the persistent docked agent bar for the current request.
/// </summary>
public interface IContactCenterAgentBarBuilder
{
    /// <summary>
    /// Builds the agent bar configuration for the current agent and page, resolving the hub URL, the workspace
    /// endpoints, the complete-activity screen-pop template (with a return URL to the current page), and the
    /// inline quick-disposition and presence-reason options.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The agent bar configuration.</returns>
    Task<AgentBarViewModel> BuildAsync(HttpContext httpContext);
}
