using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// Which AI calls a supervisor may see and act on. Supervision of people's calls is limited to the supervisor's queues
/// and campaigns, and AI calls follow the same boundary: a call placed or answered for a campaign belongs to the
/// campaign's supervisors (through its campaign queue), and a call that belongs to no campaign is seen only with
/// <see cref="ContactCenterPermissions.MonitorAllAICalls"/>, which sees every AI call.
/// </summary>
internal static class SupervisorAICallScope
{
    /// <summary>
    /// Returns whether the user may see and act on the AI call of an activity.
    /// </summary>
    /// <param name="activity">The call's activity.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="queueAuthorization">The supervisor queue authorization.</param>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<bool> IsAuthorizedAsync(
        OmnichannelActivity activity,
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService queueAuthorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (activity is null || user is null)
        {
            return false;
        }

        if (await authorizationService.AuthorizeAsync(user, ContactCenterPermissions.MonitorAllAICalls))
        {
            return true;
        }

        return !string.IsNullOrEmpty(activity.CampaignId) &&
            await queueAuthorization.IsAuthorizedAsync(
                user,
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                ContactCenterConstants.CampaignQueue.CreateId(activity.CampaignId),
                cancellationToken);
    }

    /// <summary>
    /// Returns the activities whose AI call the user may see.
    /// </summary>
    /// <param name="activities">The activities.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="queueAuthorization">The supervisor queue authorization.</param>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<IReadOnlyList<OmnichannelActivity>> FilterAsync(
        IEnumerable<OmnichannelActivity> activities,
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService queueAuthorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var visible = new List<OmnichannelActivity>();

        if (user is null)
        {
            return visible;
        }

        var all = await authorizationService.AuthorizeAsync(user, ContactCenterPermissions.MonitorAllAICalls);
        var campaigns = new Dictionary<string, bool>(StringComparer.Ordinal);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        foreach (var activity in activities ?? [])
        {
            if (all)
            {
                visible.Add(activity);

                continue;
            }

            if (string.IsNullOrEmpty(activity.CampaignId))
            {
                continue;
            }

            // The same supervisor profile answers for every call of a campaign, so it is asked once per campaign.
            if (!campaigns.TryGetValue(activity.CampaignId, out var allowed))
            {
                allowed = await queueAuthorization.IsAuthorizedAsync(user, userId, ContactCenterConstants.CampaignQueue.CreateId(activity.CampaignId), cancellationToken);
                campaigns[activity.CampaignId] = allowed;
            }

            if (allowed)
            {
                visible.Add(activity);
            }
        }

        return visible;
    }
}
