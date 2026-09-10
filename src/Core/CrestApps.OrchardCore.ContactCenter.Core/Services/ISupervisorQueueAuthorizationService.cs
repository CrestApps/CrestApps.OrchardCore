using System.Security.Claims;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Authorizes supervisor access to queue-scoped Contact Center data and operations.
/// </summary>
public interface ISupervisorQueueAuthorizationService
{
    /// <summary>
    /// Determines whether the supervisor can access a queue.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when access is allowed; otherwise <see langword="false"/>.</returns>
    Task<bool> IsAuthorizedAsync(
        ClaimsPrincipal principal,
        string userId,
        string queueId,
        CancellationToken cancellationToken = default);
}
