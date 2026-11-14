using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using YesSql;
using YesSql.Services;

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
    /// <summary>
    /// The live AI calls a supervisor may see, newest first, at most <paramref name="max"/>. The campaigns in scope are
    /// found first and the calls are filtered by them in the query, so calls out of scope never use up the limit.
    /// </summary>
    /// <param name="session">The document session.</param>
    /// <param name="max">The most calls returned.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="queueAuthorization">The supervisor queue authorization.</param>
    /// <param name="user">The supervisor.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<IReadOnlyList<OmnichannelActivity>> ListLiveAsync(
        ISession session,
        int max,
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService queueAuthorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return [];
        }

        if (await authorizationService.AuthorizeAsync(user, ContactCenterPermissions.MonitorAllAICalls))
        {
            return [.. await session
                .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                    index => index.InteractionType == ActivityInteractionType.Automated &&
                        index.Status == ActivityStatus.InProgress &&
                        index.Channel == OmnichannelConstants.Channels.Phone &&
                        !index.AiEscalated,
                    collection: OmnichannelConstants.CollectionName)
                .OrderByDescending(index => index.ScheduledUtc)
                .Take(max)
                .ListAsync(cancellationToken)];
        }

        var live = await session
            .QueryIndex<OmnichannelActivityIndex>(
                index => index.InteractionType == ActivityInteractionType.Automated &&
                    index.Status == ActivityStatus.InProgress &&
                    index.Channel == OmnichannelConstants.Channels.Phone &&
                    !index.AiEscalated,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var allowed = new List<string>();

        foreach (var campaignId in live.Select(index => index.CampaignId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await queueAuthorization.IsAuthorizedAsync(user, userId, ContactCenterConstants.CampaignQueue.CreateId(campaignId), cancellationToken))
            {
                allowed.Add(campaignId);
            }
        }

        if (allowed.Count == 0)
        {
            return [];
        }

        return [.. await session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.InteractionType == ActivityInteractionType.Automated &&
                    index.Status == ActivityStatus.InProgress &&
                    index.Channel == OmnichannelConstants.Channels.Phone &&
                    !index.AiEscalated &&
                    index.CampaignId.IsIn(allowed),
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.ScheduledUtc)
            .Take(max)
            .ListAsync(cancellationToken)];
    }

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
