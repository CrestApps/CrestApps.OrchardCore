using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Authorizes access to system-owned AI sessions through their owning omnichannel activity.
/// </summary>
public sealed class OmnichannelAIChatSessionAccessProvider : IAIChatSessionAccessProvider
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelAIChatSessionAccessProvider"/> class.
    /// </summary>
    /// <param name="activityManager">The omnichannel activity manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="contentManager">The content manager.</param>
    public OmnichannelAIChatSessionAccessProvider(
        IOmnichannelActivityManager activityManager,
        IAuthorizationService authorizationService,
        IContentManager contentManager)
    {
        _activityManager = activityManager;
        _authorizationService = authorizationService;
        _contentManager = contentManager;
    }

    /// <inheritdoc/>
    public async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        string profileId,
        string sessionId,
        string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return false;
        }

        var activity = await _activityManager.FindByIdAsync(resourceId);

        if (activity is null ||
            !string.Equals(activity.AIProfileId, profileId, StringComparison.Ordinal) ||
            !string.Equals(activity.AISessionId, sessionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, OmnichannelConstants.Permissions.ListActivities))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(activity.ContactContentItemId))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Published);

        return contact is not null &&
            await _authorizationService.AuthorizeAsync(
                user,
                OmnichannelConstants.Permissions.ListContactActivities,
                contact);
    }
}
