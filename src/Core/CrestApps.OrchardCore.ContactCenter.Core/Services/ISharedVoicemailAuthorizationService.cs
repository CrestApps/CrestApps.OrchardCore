using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides which queue shared voicemail boxes a user may see and act on.
/// </summary>
public interface ISharedVoicemailAuthorizationService
{
    /// <summary>
    /// Resolves what the user may do with the shared voicemail boxes.
    /// </summary>
    /// <param name="principal">The signed-in user.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user's access; <see cref="SharedVoicemailAccess.None"/> when they may not use the boxes.</returns>
    Task<SharedVoicemailAccess> GetAccessAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
