using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the shared fail-closed authorization boundary for call-control operations.
/// </summary>
public interface ICallControlAuthorizationService
{
    /// <summary>
    /// Authorizes a call-control operation using server-resolved call-session ownership.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The authorization result with the server-resolved provider call identifier.</returns>
    Task<CallControlAuthorizationResult> AuthorizeAsync(
        CallControlAuthorizationContext context,
        CancellationToken cancellationToken = default);
}
