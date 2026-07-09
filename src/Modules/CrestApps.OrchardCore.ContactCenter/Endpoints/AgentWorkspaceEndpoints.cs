using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class AgentWorkspaceEndpoints
{
    private const int RecentHistoryCount = 10;

    public const string StateRouteName = "ContactCenterAgentWorkspaceState";
    public const string SetPresenceRouteName = "ContactCenterAgentWorkspacePresence";
    public const string CompleteRouteName = "ContactCenterAgentWorkspaceComplete";

    public static IEndpointRouteBuilder AddAgentWorkspaceEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/contact-center/workspace/state", HandleStateAsync)
            .WithName(StateRouteName);

        builder.MapPost("Admin/contact-center/workspace/presence", HandleSetPresenceAsync)
            .WithName(SetPresenceRouteName);

        builder.MapPost("Admin/contact-center/workspace/complete", HandleCompleteAsync)
            .WithName(CompleteRouteName);

        return builder;
    }

    private static async Task<IResult> HandleStateAsync(
        IAuthorizationService authorizationService,
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IContentManager contentManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IClock clock,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return TypedResults.Forbid();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = clock.UtcNow;
        var displayName = await GetCurrentUserDisplayNameAsync(httpContext.User, userManager, displayNameProvider, httpContext.RequestAborted);

        var model = new AgentWorkspaceStateViewModel
        {
            UserId = userId,
            DisplayName = displayName,
            ServerTimeUtc = now,
        };

        var profile = await agentManager.FindByUserIdAsync(userId, httpContext.RequestAborted);

        if (profile is null)
        {
            return TypedResults.Ok(model);
        }

        model.AgentId = profile.ItemId;
        model.HasProfile = true;
        model.DisplayName = await GetUserDisplayNameAsync(profile.UserId, profile.DisplayName ?? model.DisplayName, userManager, displayNameProvider, httpContext.RequestAborted);
        model.IsSignedIn = profile.QueueIds.Count > 0 || profile.CampaignIds.Count > 0;
        model.Presence = new WorkspacePresenceViewModel
        {
            Status = profile.PresenceStatus.ToString(),
            Reason = profile.PresenceReason,
            RequestedStatus = profile.RequestedPresenceStatus?.ToString(),
        };

        foreach (var queueId in profile.QueueIds)
        {
            var queue = await queueManager.FindByIdAsync(queueId, httpContext.RequestAborted);

            if (queue is null)
            {
                continue;
            }

            var waiting = await queueItemManager.ListWaitingAsync(queueId, httpContext.RequestAborted);

            model.Queues.Add(new WorkspaceQueueStatViewModel
            {
                Id = queueId,
                Name = queue.Name,
                WaitingCount = waiting.Count,
            });
        }

        model.Offer = await BuildOfferAsync(profile.ItemId, now, reservationManager, activityManager, queueManager, contentManager, httpContext.RequestAborted);
        model.ActiveInteraction = await BuildActiveInteractionAsync(profile, authorizationService, interactionManager, activityManager, queueManager, contentManager, userManager, displayNameProvider, linkGenerator, httpContext, httpContext.RequestAborted);
        model.RecentHistory = await BuildRecentHistoryAsync(profile.ItemId, interactionManager, httpContext.RequestAborted);

        return TypedResults.Ok(model);
    }

    private static async Task<IResult> HandleSetPresenceAsync(
        [FromForm] SetPresenceRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IAgentPresenceManager presenceManager,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        await presenceManager.SetPresenceAsync(userId, request.Status, request.Reason, httpContext.RequestAborted);

        return TypedResults.Ok();
    }

    private static async Task<IResult> HandleCompleteAsync(
        [FromForm] CompleteRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IOmnichannelActivityManager activityManager,
        IInteractionManager interactionManager,
        IActivityDispositionService dispositionService,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(request.ActivityId))
        {
            return TypedResults.BadRequest();
        }

        var activity = await activityManager.FindByIdAsync(request.ActivityId, httpContext.RequestAborted);

        if (activity is null)
        {
            return TypedResults.NotFound();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        var profile = await agentManager.FindByUserIdAsync(userId, httpContext.RequestAborted);

        if (profile is null || !await AgentOwnsWorkAsync(profile, request.ActivityId, interactionManager, activityManager, httpContext.RequestAborted))
        {
            return TypedResults.Forbid();
        }

        activity.DispositionId = request.DispositionId;

        var result = await dispositionService.ApplyAsync(new ActivityDispositionRequest
        {
            Activity = activity,
            DispositionId = request.DispositionId,
            Notes = request.Notes,
            Source = ActivityDispositionSource.Agent,
            ActorId = userId,
            ActorDisplayName = await GetCurrentUserDisplayNameAsync(httpContext.User, userManager, displayNameProvider, httpContext.RequestAborted),
        }, httpContext.RequestAborted);

        if (result.Succeeded)
        {
            await presenceManager.CompleteWorkAsync(profile.ItemId, httpContext.RequestAborted);
        }

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.ErrorMessage,
        });
    }

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
            CustomerLabel = await ResolveCustomerLabelAsync(activity, null, contentManager, cancellationToken),
            CustomerAddress = activity?.PreferredDestination,
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = now,
        };
    }

    private static async Task<WorkspaceActiveInteractionViewModel> BuildActiveInteractionAsync(
        AgentProfile profile,
        IAuthorizationService authorizationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IActivityQueueManager queueManager,
        IContentManager contentManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var interaction = await interactionManager.FindActiveByAgentAsync(profile.ItemId, cancellationToken);

        if (interaction is null)
        {
            interaction = await FindPendingWrapUpInteractionAsync(profile, interactionManager, activityManager, cancellationToken);
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
            CustomerLabel = await ResolveCustomerLabelAsync(activity, interaction.CustomerAddress, contentManager, cancellationToken),
            CustomerAddress = interaction.CustomerAddress,
            QueueName = queue?.Name,
            ContactUrl = BuildContactUrl(activity, linkGenerator, httpContext),
            CompleteUrl = await BuildCompleteActivityUrlAsync(activity, authorizationService, linkGenerator, httpContext, cancellationToken),
            StartedUtc = interaction.StartedUtc,
            AnsweredUtc = interaction.AnsweredUtc,
        };
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

        var wrapUpInteraction = await FindPendingWrapUpInteractionAsync(profile, interactionManager, activityManager, cancellationToken);

        return string.Equals(wrapUpInteraction?.ActivityItemId, activityId, StringComparison.Ordinal);
    }

    private static async Task<Interaction> FindPendingWrapUpInteractionAsync(
        AgentProfile profile,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        CancellationToken cancellationToken)
    {
        var interactions = await interactionManager.ListRecentByAgentAsync(profile.ItemId, RecentHistoryCount, cancellationToken);

        foreach (var interaction in interactions)
        {
            if (interaction.Status is not InteractionStatus.Ended and not InteractionStatus.Failed)
            {
                continue;
            }

            if (string.IsNullOrEmpty(interaction.ActivityItemId))
            {
                continue;
            }

            var activity = await activityManager.FindByIdAsync(interaction.ActivityItemId, cancellationToken);

            if (activity is null ||
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

    private static async Task<IList<WorkspaceHistoryEntryViewModel>> BuildRecentHistoryAsync(
        string agentId,
        IInteractionManager interactionManager,
        CancellationToken cancellationToken)
    {
        var interactions = await interactionManager.ListRecentByAgentAsync(agentId, RecentHistoryCount, cancellationToken);

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
        IContentManager contentManager,
        CancellationToken cancellationToken)
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
        HttpContext httpContext,
        CancellationToken cancellationToken)
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
            new { area = OmnichannelConstants.Features.Managements, id = activity.ItemId });
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
            return await GetUserDisplayNameAsync(currentUser, user.Identity?.Name, displayNameProvider, cancellationToken);
        }

        return user.Identity?.Name;
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
    }
}
