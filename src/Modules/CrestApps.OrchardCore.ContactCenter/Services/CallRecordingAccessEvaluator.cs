using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Works out which recorded calls a user may hear from the call recording permissions.
/// </summary>
public sealed class CallRecordingAccessEvaluator
{
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingAccessEvaluator"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    public CallRecordingAccessEvaluator(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the recorded calls a user may hear.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The user's access; <see cref="CallRecordingAccess.None"/> when there is no user.</returns>
    public async Task<CallRecordingAccess> GetAccessAsync(ClaimsPrincipal user)
    {
        if (user is null)
        {
            return CallRecordingAccess.None;
        }

        // Hearing everyone's calls includes hearing one's own.
        var everyone = await _authorizationService.AuthorizeAsync(user, ContactCenterPermissions.ListenToAllCallRecordings);

        return new CallRecordingAccess(
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            everyone || await _authorizationService.AuthorizeAsync(user, ContactCenterPermissions.ListenToOwnCallRecordings),
            everyone);
    }
}
