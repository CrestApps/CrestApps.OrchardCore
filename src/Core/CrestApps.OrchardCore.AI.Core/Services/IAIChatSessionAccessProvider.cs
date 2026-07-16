using System.Security.Claims;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Provides resource-specific authorization for system-owned AI chat sessions.
/// </summary>
public interface IAIChatSessionAccessProvider
{
    /// <summary>
    /// Determines whether the current user may review a system-owned AI chat session.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="profileId">The AI profile identifier.</param>
    /// <param name="sessionId">The AI chat session identifier.</param>
    /// <param name="resourceId">The external resource that owns the session.</param>
    /// <returns><see langword="true"/> when access is allowed; otherwise, <see langword="false"/>.</returns>
    Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        string profileId,
        string sessionId,
        string resourceId);
}
