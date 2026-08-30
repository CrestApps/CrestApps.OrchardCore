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

internal static class AgentWorkspaceEndpoints
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

        if (!await authorizationService.AuthorizeAsync(
            httpContext.User,
            OmnichannelConstants.Permissions.CompleteActivity,
            activity))
        {
            return TypedResults.Forbid();
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
            return TypedResults.Forbid();
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
            return TypedResults.Forbid();
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

    /// <summary>
    /// Streams a voicemail recording to the agent it was left for. The recipient check restricts playback to the
    /// owning agent, and every grant is routed through the recording-access governance service so it is authorized
    /// and written to the recording-access audit trail before any media is opened.
    /// </summary>
    internal static async Task<IResult> HandleVoicemailMediaAsync(
        string interactionId,
        IInteractionManager interactionManager,
        IAgentProfileManager agentProfileManager,
        HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Challenge();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(interactionId))
        {
            return TypedResults.Forbid();
        }

        // A voicemail that will not play returns 404 through several distinct branches; logging the specific reason
        // turns an otherwise silent "nothing happens" into a diagnosable cause (a missing recording, a governance
        // feature that is not enabled, media that has not finished ingesting, and so on).
        var logger = httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("ContactCenterVoicemailMedia");

        void LogNotAvailable(string reason)
        {
            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug("Voicemail media for interaction {InteractionId} is unavailable: {Reason}.", interactionId.SanitizeLogValue(), reason);
            }
        }

        var interaction = await interactionManager.FindByIdAsync(interactionId, httpContext.RequestAborted);

        // Only a voicemail interaction is playable through this endpoint; a normal call recording is governed and
        // surfaced elsewhere, so this endpoint deliberately refuses to expose it.
        if (interaction is null || !IsVoicemailInteraction(interaction))
        {
            LogNotAvailable(interaction is null ? "the interaction was not found" : "the interaction is not flagged as a voicemail");

            return TypedResults.NotFound();
        }

        // The voicemail may be played only by the agent it was left for. The recipient's agent-profile id is stamped
        // on the interaction when it is sent to voicemail, so resolve it and compare the owning user.
        var recipientAgentId = ResolveVoicemailRecipientAgentId(interaction);
        var recipientAgent = string.IsNullOrEmpty(recipientAgentId)
            ? null
            : await agentProfileManager.FindByIdAsync(recipientAgentId, httpContext.RequestAborted);

        if (recipientAgent is null || !string.Equals(recipientAgent.UserId, userId, StringComparison.Ordinal))
        {
            LogNotAvailable(recipientAgent is null
                ? "the voicemail has no resolvable recipient agent"
                : "the requesting user is not the voicemail recipient");

            return TypedResults.Forbid();
        }

        // The recording may not have finished ingesting yet (the caller just hung up, or the durable ingest job has
        // not run). Treat that as "not yet available" rather than an error.
        if (string.IsNullOrEmpty(interaction.RecordingReference) ||
            interaction.TechnicalMetadata is null ||
            !interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var storageReferenceValue) ||
            storageReferenceValue?.ToString() is not { Length: > 0 } storageReference)
        {
            LogNotAvailable("the interaction carries no recording storage reference yet");

            return TypedResults.NotFound();
        }

        // Gate and audit the access. RecordAccessAsync writes the RecordingAccessed audit event and returns false
        // when there is no recording to access, so playback shares the same governance trail as any other recording.
        // The governance service and the media store are owned by the recording feature; when it is not enabled
        // there is nothing to play, so treat a missing service as "not available" rather than failing hard.
        var recordingAccessGovernanceService = httpContext.RequestServices.GetService<IRecordingAccessGovernanceService>();

        if (recordingAccessGovernanceService is null)
        {
            LogNotAvailable("the recording governance service is not registered (the Recording Governance feature is not enabled)");

            return TypedResults.NotFound();
        }

        var granted = await recordingAccessGovernanceService.RecordAccessAsync(
            interactionId,
            userId,
            "voicemail-playback",
            httpContext.RequestAborted);

        if (!granted)
        {
            LogNotAvailable("recording access was not granted by governance");

            return TypedResults.NotFound();
        }

        var mediaStore = httpContext.RequestServices.GetService<IRecordingMediaStore>();

        if (mediaStore is null)
        {
            LogNotAvailable("no recording media store is registered");

            return TypedResults.NotFound();
        }

        var stream = await mediaStore.OpenReadAsync(storageReference, httpContext.RequestAborted);

        if (stream is null)
        {
            LogNotAvailable("the media store has no bytes for the recording storage reference");

            return TypedResults.NotFound();
        }

        return Results.Stream(stream, "audio/mpeg");
    }

    internal static async Task<IResult> HandleDeleteVoicemailAsync(
        string interactionId,
        IInteractionManager interactionManager,
        IAgentProfileManager agentProfileManager,
        ITelephonyInteractionStore telephonyInteractionStore,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Challenge();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(interactionId))
        {
            return TypedResults.Forbid();
        }

        // Deleting a voicemail changes state, so it is a POST guarded by antiforgery, unlike the media GET.
        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var interaction = await interactionManager.FindByIdAsync(interactionId, httpContext.RequestAborted);

        if (interaction is null || !IsVoicemailInteraction(interaction))
        {
            return TypedResults.NotFound();
        }

        // A voicemail may be deleted only by the agent it was left for -- the same ownership rule as playback.
        var recipientAgentId = ResolveVoicemailRecipientAgentId(interaction);
        var recipientAgent = string.IsNullOrEmpty(recipientAgentId)
            ? null
            : await agentProfileManager.FindByIdAsync(recipientAgentId, httpContext.RequestAborted);

        if (recipientAgent is null || !string.Equals(recipientAgent.UserId, userId, StringComparison.Ordinal))
        {
            return TypedResults.Forbid();
        }

        // Delete the encrypted media first, while the storage reference is still on the interaction. The media store
        // is owned by the Telephony recording feature; a governance erase alone would not remove the bytes here
        // because the media-deletion event handler lives in the (separately enabled) full call-recording feature.
        if (interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var storageReferenceValue) &&
            storageReferenceValue?.ToString() is { Length: > 0 } storageReference)
        {
            var mediaStore = httpContext.RequestServices.GetService<IRecordingMediaStore>();

            if (mediaStore is not null)
            {
                await mediaStore.DeleteAsync(storageReference, httpContext.RequestAborted);
            }
        }

        // Then run the recording-governance erase (audit trail + clearing the interaction's retrieval handle and
        // erasure tombstone) when the governance service is available, so a later playback cannot resurrect it.
        var governance = httpContext.RequestServices.GetService<IRecordingAccessGovernanceService>();

        if (governance is not null)
        {
            await governance.EraseAsync(interactionId, userId, "voicemail-deleted", httpContext.RequestAborted);
        }

        // Finally remove the soft-phone projection so the voicemail leaves the recipient's inbox. It is keyed by the
        // provider call id, which the projection mirrors from the interaction.
        if (!string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            var projection = await telephonyInteractionStore.FindByCallIdAsync(userId, interaction.ProviderInteractionId, httpContext.RequestAborted);

            if (projection is not null)
            {
                await telephonyInteractionStore.DeleteAsync(projection, httpContext.RequestAborted);
            }
        }

        return TypedResults.Ok();
    }

    private static bool IsVoicemailInteraction(Interaction interaction)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.ProjectionMetadataKey, out var value) &&
            (value is bool boolean ? boolean : bool.TryParse(value?.ToString(), out var parsed) && parsed);
    }

    private static string ResolveVoicemailRecipientAgentId(Interaction interaction)
    {
        if (interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.RecipientAgentMetadataKey, out var recipient) &&
            recipient?.ToString() is { Length: > 0 } recipientAgentId)
        {
            return recipientAgentId;
        }

        return interaction.AgentId;
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
