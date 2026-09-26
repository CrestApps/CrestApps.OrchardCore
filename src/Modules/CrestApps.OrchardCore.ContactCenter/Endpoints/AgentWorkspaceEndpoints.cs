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

internal static partial class AgentWorkspaceEndpoints
{
    private const int RecentHistoryCount = 10;

    public const string StateRouteName = "ContactCenterAgentWorkspaceState";
    public const string SetPresenceRouteName = "ContactCenterAgentWorkspacePresence";
    public const string CompleteRouteName = "ContactCenterAgentWorkspaceComplete";
    public const string PauseRecordingRouteName = "ContactCenterAgentWorkspacePauseRecording";
    public const string ResumeRecordingRouteName = "ContactCenterAgentWorkspaceResumeRecording";
    public const string VoicemailMediaRouteName = "ContactCenterVoicemailMedia";
    public const string VoicemailDeleteRouteName = "ContactCenterVoicemailDelete";

    public static IEndpointRouteBuilder AddAgentWorkspaceEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/contact-center/voicemail/{interactionId}/media", HandleVoicemailMediaAsync)
            .WithName(VoicemailMediaRouteName);

        builder.MapPost("Admin/contact-center/voicemail/{interactionId}/delete", HandleDeleteVoicemailAsync)
            .WithName(VoicemailDeleteRouteName);

        builder.MapGet("Admin/contact-center/workspace/state", HandleStateAsync)
            .WithName(StateRouteName);

        builder.MapPost("Admin/contact-center/workspace/presence", HandleSetPresenceAsync)
            .WithName(SetPresenceRouteName);

        builder.MapPost("Admin/contact-center/workspace/complete", HandleCompleteAsync)
            .WithName(CompleteRouteName);

        builder.MapPost("Admin/contact-center/workspace/recording/pause", HandlePauseRecordingAsync)
            .WithName(PauseRecordingRouteName);

        builder.MapPost("Admin/contact-center/workspace/recording/resume", HandleResumeRecordingAsync)
            .WithName(ResumeRecordingRouteName);

        return builder;
    }

    /// <summary>
    /// Builds the agent workspace state a signed-in agent polls. Exposed to the test assembly so the number of
    /// round trips a single poll issues can be asserted directly: the batching this handler relies on lives in
    /// the stores, and a caller that looped over the single-item APIs instead would produce identical output.
    /// </summary>
    internal static async Task<IResult> HandleStateAsync(
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
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IClock clock,
        IOptions<AgentAvailabilityOptions> availabilityOptions,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return ContactCenterApiResults.Forbidden();
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
            HasActiveReservation = !string.IsNullOrEmpty(profile.ActiveReservationId),
        };

        var queueIds = profile.QueueIds;
        var waitingCounts = queueIds.Count == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await queueItemManager.CountWaitingByQueueIdsAsync([.. queueIds], httpContext.RequestAborted);

        foreach (var queueId in queueIds)
        {
            // Queues are configuration-catalog backed, so resolving them one at a time reads an already
            // materialized document rather than issuing a query per queue.
            var queue = await queueManager.FindByIdAsync(queueId, httpContext.RequestAborted);

            if (queue is null)
            {
                continue;
            }

            model.Queues.Add(new WorkspaceQueueStatViewModel
            {
                Id = queueId,
                Name = queue.Name,
                WaitingCount = waitingCounts.TryGetValue(queueId, out var waitingCount) ? waitingCount : 0,
            });
        }

        // Read once and share. The active interaction and the history panel are built from the same recent
        // interactions, so reading them per panel would run the same query twice on every poll.
        var recentInteractions = await interactionManager.GetRecentByAgentAsync(profile.ItemId, RecentHistoryCount, httpContext.RequestAborted);

        model.Offer = await BuildOfferAsync(profile.ItemId, now, reservationManager, activityManager, queueManager, contentManager, httpContext.RequestAborted);
        model.ActiveInteraction = await BuildActiveInteractionAsync(profile, recentInteractions, now, availabilityOptions.Value.MaximumWrapUpDuration, authorizationService, interactionManager, activityManager, queueManager, contentManager, voiceProviderResolver, linkGenerator, httpContext, httpContext.RequestAborted);
        model.RecentHistory = BuildRecentHistory(recentInteractions);

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
            return ContactCenterApiResults.Forbidden();
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
        IOmnichannelActivityManager activityManager,
        IInteractionManager interactionManager,
        IActivityDispositionService dispositionService,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return ContactCenterApiResults.Forbidden();
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

        if (!await authorizationService.AuthorizeAsync(
            httpContext.User,
            OmnichannelConstants.Permissions.CompleteActivity,
            activity))
        {
            return ContactCenterApiResults.Forbidden();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return ContactCenterApiResults.Forbidden();
        }

        var profile = await agentManager.FindByUserIdAsync(userId, httpContext.RequestAborted);

        if (profile is null || !await AgentOwnsWorkAsync(profile, request.ActivityId, interactionManager, activityManager, httpContext.RequestAborted))
        {
            return ContactCenterApiResults.Forbidden();
        }

        activity.DispositionId = request.DispositionId;

        var result = await dispositionService.ApplyAsync(new ActivityDispositionRequest
        {
            Activity = activity,
            DispositionId = request.DispositionId,
            Notes = request.Notes,
            ActionScheduleDates = request.ActionScheduleDates,
            Source = ActivityDispositionSource.Agent,
            ActorId = userId,
            ActorDisplayName = await GetCurrentUserDisplayNameAsync(httpContext.User, userManager, displayNameProvider, httpContext.RequestAborted),
        }, httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.ErrorMessage,
        });
    }

    private static async Task<IResult> HandlePauseRecordingAsync(
        [FromForm] RecordingControlRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IAgentRecordingControlService> recordingControlServices,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SecurePauseRecording))
        {
            return ContactCenterApiResults.Forbidden();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(request.InteractionId))
        {
            return TypedResults.BadRequest();
        }

        var recordingControlService = recordingControlServices.FirstOrDefault();

        if (recordingControlService is null)
        {
            return TypedResults.NotFound();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await recordingControlService.PauseAsync(
            request.InteractionId,
            userId,
            httpContext.User,
            request.Reason,
            httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.OutcomeUnknown,
            result.Reason,
            result.IsPaused,
        });
    }

    private static async Task<IResult> HandleResumeRecordingAsync(
        [FromForm] RecordingControlRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IAgentRecordingControlService> recordingControlServices,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SecurePauseRecording))
        {
            return ContactCenterApiResults.Forbidden();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(request.InteractionId))
        {
            return TypedResults.BadRequest();
        }

        var recordingControlService = recordingControlServices.FirstOrDefault();

        if (recordingControlService is null)
        {
            return TypedResults.NotFound();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await recordingControlService.ResumeAsync(
            request.InteractionId,
            userId,
            httpContext.User,
            httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.OutcomeUnknown,
            result.Reason,
            result.IsPaused,
        });
    }
}
