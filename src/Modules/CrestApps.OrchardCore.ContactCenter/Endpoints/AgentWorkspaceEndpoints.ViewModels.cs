using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// What the agent workspace actually shows: the offer card, the live-call panel, the labels and links on them.
/// <para>
/// Separated from the endpoints themselves, which are about routing, authorization and the shape of the
/// response. The two change for different reasons - a new field on the offer card is not a change to who may
/// call the endpoint - and together they were the largest file in the module.
/// </para>
/// </summary>
internal static partial class AgentWorkspaceEndpoints
{
    private static async Task<WorkspaceOfferViewModel> BuildOfferAsync(
        string agentId,
        DateTime now,
        IActivityReservationManager reservationManager,
        IOmnichannelActivityManager activityManager,
        IActivityQueueManager queueManager,
        IContentManager contentManager,
        CancellationToken cancellationToken)
    {
        var reservation = await reservationManager.FindPendingByAgentAsync(agentId, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        var activity = await activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);
        var queue = string.IsNullOrEmpty(reservation.QueueId)
            ? null
            : await queueManager.FindByIdAsync(reservation.QueueId, cancellationToken);

        return new WorkspaceOfferViewModel
        {
            ReservationId = reservation.ItemId,
            ActivityItemId = reservation.ActivityItemId,
            QueueId = reservation.QueueId,
            QueueName = queue?.Name,
            CustomerLabel = await ResolveCustomerLabelAsync(activity, null, contentManager),
            CustomerAddress = activity?.PreferredDestination,
            AutoOpenActivity = DialerActivitySourceHelper.IsDialerSource(activity?.Source),
            Kind = AgentOfferKindHelper.FromActivitySource(activity?.Source),
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = now,
        };
    }

    private static async Task<WorkspaceActiveInteractionViewModel> BuildActiveInteractionAsync(
        AgentProfile profile,
        IReadOnlyCollection<Interaction> recentInteractions,
        DateTime now,
        TimeSpan wrapUpWindow,
        IAuthorizationService authorizationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IActivityQueueManager queueManager,
        IContentManager contentManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var interaction = await interactionManager.FindActiveByAgentAsync(profile.ItemId, cancellationToken);

        if (interaction is null)
        {
            // Bound the post-call wrap-up the bar shows to the wrap-up window. Past that, the interaction is no
            // longer the agent's live after-call work (the availability recovery pass would have closed it out),
            // so a call finished a while ago must not keep sitting in the bar demanding completion.
            interaction = await FindPendingWrapUpInteractionAsync(profile, recentInteractions, activityManager, now, wrapUpWindow, cancellationToken);
        }

        if (interaction is null)
        {
            return null;
        }

        var activity = string.IsNullOrEmpty(interaction.ActivityItemId)
            ? null
            : await activityManager.FindByIdAsync(interaction.ActivityItemId, cancellationToken);
        var queue = string.IsNullOrEmpty(interaction.QueueId)
            ? null
            : await queueManager.FindByIdAsync(interaction.QueueId, cancellationToken);

        return new WorkspaceActiveInteractionViewModel
        {
            InteractionId = interaction.ItemId,
            ActivityItemId = interaction.ActivityItemId,
            Direction = interaction.Direction.ToString(),
            Status = interaction.Status.ToString(),
            CustomerLabel = await ResolveCustomerLabelAsync(activity, interaction.CustomerAddress, contentManager),
            CustomerAddress = interaction.CustomerAddress,
            QueueName = queue?.Name,
            ContactUrl = BuildContactUrl(activity, linkGenerator, httpContext),
            CompleteUrl = await BuildCompleteActivityUrlAsync(activity, authorizationService, linkGenerator, httpContext),
            StartedUtc = interaction.StartedUtc,
            AnsweredUtc = interaction.AnsweredUtc,
            RecordingState = interaction.RecordingState.ToString(),
            IsRecordingPaused = interaction.RecordingState == RecordingState.Paused,
            SupportsSecurePause = SupportsSecurePause(interaction, voiceProviderResolver),
        };
    }

    private static bool SupportsSecurePause(
        Interaction interaction,
        IContactCenterVoiceProviderResolver voiceProviderResolver)
    {
        var provider = voiceProviderResolver.Get(interaction.ProviderName);

        return provider is IContactCenterVoiceRecordingProvider &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording) &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.RecordingPause) &&
            !string.IsNullOrEmpty(interaction.ProviderInteractionId);
    }

    private static async Task<bool> AgentOwnsWorkAsync(
        AgentProfile profile,
        string activityId,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        CancellationToken cancellationToken)
    {
        var activeInteraction = await interactionManager.FindActiveByAgentAsync(profile.ItemId, cancellationToken);

        if (string.Equals(activeInteraction?.ActivityItemId, activityId, StringComparison.Ordinal))
        {
            return true;
        }

        var recentInteractions = await interactionManager.GetRecentByAgentAsync(profile.ItemId, RecentHistoryCount, cancellationToken);
        // No recency bound here: this authorizes the agent to complete work they genuinely handled, which stays
        // valid even after the bar has stopped surfacing the wrap-up prompt for it.
        var wrapUpInteraction = await FindPendingWrapUpInteractionAsync(profile, recentInteractions, activityManager, now: default, wrapUpWindow: null, cancellationToken);

        return string.Equals(wrapUpInteraction?.ActivityItemId, activityId, StringComparison.Ordinal);
    }

    // A just-ended call is live after-call work only within the wrap-up window; past it the availability recovery
    // pass would have closed the wrap-up out, so the bar must stop offering it too. A null window disables the
    // bound (used by the completion-authorization path, which stays valid regardless of how long ago the call ended).
    private static bool IsWithinWrapUpWindow(Interaction interaction, DateTime now, TimeSpan? wrapUpWindow)
    {
        if (wrapUpWindow is null)
        {
            return true;
        }

        var endedUtc = interaction.EndedUtc ?? interaction.ModifiedUtc ?? interaction.CreatedUtc;

        return endedUtc + wrapUpWindow.Value >= now;
    }

    private static async Task<Interaction> FindPendingWrapUpInteractionAsync(
        AgentProfile profile,
        IReadOnlyCollection<Interaction> recentInteractions,
        IOmnichannelActivityManager activityManager,
        DateTime now,
        TimeSpan? wrapUpWindow,
        CancellationToken cancellationToken)
    {
        // Wrap-up (disposition) applies only to a call the agent actually handled: it must have ended normally and
        // have been answered. A failed or never-answered call — an unanswered inbound ring, a busy/failed dial —
        // was not handled, so it must never linger in the bar or workspace demanding an activity completion.
        // When a wrap-up window is supplied, a call that ended longer ago than that window is no longer live
        // after-call work and is dropped, so a stale record cannot stick around indefinitely.
        var candidates = recentInteractions
            .Where(interaction => interaction.Status == InteractionStatus.Ended &&
                interaction.AnsweredUtc.HasValue &&
                !string.IsNullOrEmpty(interaction.ActivityItemId) &&
                IsWithinWrapUpWindow(interaction, now, wrapUpWindow))
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        // Every candidate names an activity, and the answer depends on that activity. Resolving them one at a
        // time would make the number of queries this poll issues depend on how much work the agent has just
        // finished, so they are resolved together and matched in memory.
        var activityIds = candidates
            .Select(interaction => interaction.ActivityItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var activities = await activityManager.GetByIdsAsync(activityIds, cancellationToken);
        var activitiesById = activities.ToDictionary(activity => activity.ItemId, StringComparer.Ordinal);

        foreach (var interaction in candidates)
        {
            if (!activitiesById.TryGetValue(interaction.ActivityItemId, out var activity) ||
                activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged)
            {
                continue;
            }

            if (string.Equals(activity.AssignedToId, profile.UserId, StringComparison.Ordinal) ||
                string.Equals(interaction.AgentId, profile.ItemId, StringComparison.Ordinal))
            {
                return interaction;
            }
        }

        return null;
    }

    private static IList<WorkspaceHistoryEntryViewModel> BuildRecentHistory(IReadOnlyCollection<Interaction> interactions)
    {
        return [.. interactions.Select(interaction => new WorkspaceHistoryEntryViewModel
        {
            InteractionId = interaction.ItemId,
            Direction = interaction.Direction.ToString(),
            Status = interaction.Status.ToString(),
            CustomerLabel = interaction.CustomerAddress,
            CreatedUtc = interaction.CreatedUtc,
            EndedUtc = interaction.EndedUtc,
        })];
    }

    private static async Task<string> ResolveCustomerLabelAsync(
        OmnichannelActivity activity,
        string fallback,
        IContentManager contentManager)
    {
        if (activity is not null && !string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            var contact = await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null && !string.IsNullOrEmpty(contact.DisplayText))
            {
                return contact.DisplayText;
            }
        }

        return string.IsNullOrEmpty(fallback) ? activity?.PreferredDestination : fallback;
    }

    private static string BuildContactUrl(
        OmnichannelActivity activity,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        if (activity is null || string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return null;
        }

        return linkGenerator.GetPathByAction(
            httpContext,
            "Edit",
            "Admin",
            new { area = "OrchardCore.Contents", contentItemId = activity.ContactContentItemId });
    }

    private static async Task<string> BuildCompleteActivityUrlAsync(
        OmnichannelActivity activity,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        if (activity is null ||
            string.IsNullOrEmpty(activity.ItemId) ||
            activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged ||
            !await authorizationService.AuthorizeAsync(httpContext.User, OmnichannelConstants.Permissions.CompleteActivity, activity))
        {
            return null;
        }

        return linkGenerator.GetPathByAction(
            httpContext,
            "Complete",
            "Activities",
            new
            {
                area = OmnichannelConstants.Features.Managements,
                id = activity.ItemId,
                returnUrl = linkGenerator.GetPathByAction(
                    httpContext,
                    "Index",
                    "AgentWorkspace",
                    new { area = ContactCenterConstants.Feature.Area }),
            });
    }

    private static async Task<string> GetCurrentUserDisplayNameAsync(
        ClaimsPrincipal user,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        var currentUser = await userManager.GetUserAsync(user);

        if (currentUser is not null)
        {
            return await GetUserDisplayNameAsync(currentUser, "Unknown user", displayNameProvider, cancellationToken);
        }

        return "Unknown user";
    }

    private static async Task<string> GetUserDisplayNameAsync(
        string userId,
        string fallback,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return fallback;
        }

        var user = await userManager.FindByIdAsync(userId);

        return await GetUserDisplayNameAsync(user, fallback, displayNameProvider, cancellationToken);
    }

    private static async Task<string> GetUserDisplayNameAsync(
        IUser user,
        string fallback,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        if (user is not null)
        {
            var displayName = await displayNameProvider.GetAsync(user, cancellationToken);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }

        return fallback;
    }

    private sealed class SetPresenceRequest
    {
        public AgentPresenceStatus Status { get; set; }

        public string Reason { get; set; }
    }

    private sealed class CompleteRequest
    {
        public string ActivityId { get; set; }

        public string DispositionId { get; set; }

        public string Notes { get; set; }

        public IDictionary<string, DateTime?> ActionScheduleDates { get; set; }
    }

    private sealed class RecordingControlRequest
    {
        public string InteractionId { get; set; }

        public string Reason { get; set; }
    }
}
