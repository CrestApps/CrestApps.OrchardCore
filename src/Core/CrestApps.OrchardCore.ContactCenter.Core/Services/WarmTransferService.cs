using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using ProviderVoiceEvent = CrestApps.OrchardCore.Telephony.Models.ProviderVoiceEvent;
using VoiceCallState = CrestApps.OrchardCore.Telephony.Models.VoiceCallState;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
public sealed class WarmTransferService : IWarmTransferService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly ICallControlAuthorizationService _authorizationService;
    private readonly ITransferDestinationResolver _destinationResolver;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IVoiceMediaItemManager _mediaItemManager;
    private readonly IConsultTransferService _consults;
    private readonly ITransferAgentReleaseService _agentRelease;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WarmTransferService"/> class.
    /// </summary>
    public WarmTransferService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        ICallControlAuthorizationService authorizationService,
        ITransferDestinationResolver destinationResolver,
        IAgentProfileManager agentManager,
        IAgentAvailabilityService availabilityService,
        IActivityQueueManager queueManager,
        IVoiceMediaItemManager mediaItemManager,
        IConsultTransferService consults,
        ITransferAgentReleaseService agentRelease,
        IAgentPresenceManager presenceManager,
        IProviderVoiceEventService providerVoiceEventService,
        IContactCenterEventPublisher publisher,
        ISession session,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _authorizationService = authorizationService;
        _destinationResolver = destinationResolver;
        _agentManager = agentManager;
        _availabilityService = availabilityService;
        _queueManager = queueManager;
        _mediaItemManager = mediaItemManager;
        _consults = consults;
        _agentRelease = agentRelease;
        _presenceManager = presenceManager;
        _providerVoiceEventService = providerVoiceEventService;
        _publisher = publisher;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<WarmTransferResult> StartAsync(WarmTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A queue is not somebody to talk to. Consulting one would hold the caller while the agent listened to the
        // queue's own hold music, so a queue only takes a blind transfer.
        if (request.TargetType is not InteractionTransferTargetType.Agent and not InteractionTransferTargetType.External)
        {
            return WarmTransferResult.Failure("A warm transfer goes to an agent or an external number. Send the call to a queue with a blind transfer.");
        }

        var (interaction, authorization, failure) = await AuthorizeAsync(request.InteractionId, request.UserId, request.Principal, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        var destination = await _destinationResolver.ResolveAsync(new TransferRequest
        {
            InteractionId = interaction.ItemId,
            Type = InteractionTransferType.Consultative,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            InitiatedByAgentId = authorization.AgentId,
            InitiatedByUserId = request.UserId,
            Principal = request.Principal,
        }, request.Principal, cancellationToken);

        if (!destination.Succeeded)
        {
            await _publisher.PublishAsync(
                TransferEventFactory.Denied(interaction, authorization.AgentId, request.UserId, request.TargetType, destination.FailureReason, _clock.UtcNow),
                cancellationToken);

            return WarmTransferResult.Failure(destination.FailureReason);
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ContactCenterConstants.AttendedTransferMetadata.TargetType] = destination.TargetType.ToString(),
        };

        var targetAddress = destination.ResolvedTarget;

        if (destination.TargetType == InteractionTransferTargetType.Agent)
        {
            var target = await _agentManager.FindByIdAsync(destination.ResolvedTarget, cancellationToken);

            if (target is null || string.IsNullOrEmpty(target.UserId))
            {
                return WarmTransferResult.Failure("The agent could not be found.");
            }

            if (string.Equals(target.ItemId, authorization.AgentId, StringComparison.Ordinal))
            {
                return WarmTransferResult.Failure("A call cannot be transferred to the agent who is already on it.");
            }

            if ((await _availabilityService.GetForDirectAsync(target.ItemId, cancellationToken))?.Agent is null)
            {
                return WarmTransferResult.Failure($"{DisplayName(target)} is not available to take a consult.");
            }

            // The provider rings the agent wherever they are signed in, which only their user id identifies.
            metadata[ContactCenterConstants.AttendedTransferMetadata.AgentUserId] = target.UserId;
            targetAddress = DisplayName(target);
        }

        var holdAudio = await ResolveHoldAudioAsync(interaction.QueueId, cancellationToken);

        if (!string.IsNullOrEmpty(holdAudio))
        {
            metadata[ContactCenterConstants.AttendedTransferMetadata.HoldAudio] = holdAudio;
        }

        var consult = await _consults.StartAsync(new ConsultTransferRequest
        {
            CallSessionId = authorization.CallSession.ItemId,
            InitiatedByAgentId = authorization.AgentId,
            TargetType = destination.TargetType,
            TargetId = destination.ResolvedTarget,
            TargetAddress = targetAddress,
            Metadata = metadata,
        }, cancellationToken);

        if (consult is null)
        {
            return WarmTransferResult.Failure("The consult could not be started. The caller is still with you.");
        }

        InteractionTransferHistory.Open(interaction, authorization.AgentId, destination.TargetType, destination.ResolvedTarget, consult.StartedUtc, InteractionTransferHistory.Consulting);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return WarmTransferResult.For(consult, "The caller is on hold while the destination rings.");
    }

    /// <inheritdoc />
    public async Task<WarmTransferResult> CompleteAsync(WarmTransferCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (interaction, authorization, failure) = await AuthorizeAsync(command.InteractionId, command.UserId, command.Principal, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        var consult = FindConsult(authorization.CallSession, authorization.AgentId, command.ConsultId);

        if (consult is null)
        {
            return WarmTransferResult.Failure("There is no consult on this call to complete.");
        }

        if (!await _consults.CompleteAsync(authorization.CallSession.ItemId, consult.ConsultId, cancellationToken))
        {
            return WarmTransferResult.Failure("The transfer can be completed once the destination has answered.");
        }

        var now = _clock.UtcNow;
        var session = await _callSessionManager.FindByIdAsync(authorization.CallSession.ItemId, cancellationToken);
        var transferringAgentId = authorization.AgentId;
        var agentLegs = await _agentRelease.ReleaseAsync(interaction, session, transferringAgentId, now, cancellationToken);
        var toAgent = consult.TargetType == InteractionTransferTargetType.Agent;

        // The consulted party's leg does not end with the consult; it is how they are on the call from now on.
        CallTopologyProjector.HandOverToConsultedParty(
            session,
            consult.ConsultId,
            toAgent ? CallPartyRole.Agent : CallPartyRole.External,
            toAgent ? consult.TargetId : null,
            now);

        if (toAgent)
        {
            session.AgentId = consult.TargetId;
            interaction.AgentId = consult.TargetId;
        }
        else
        {
            // Nobody in the contact center takes the call over, so it stays credited to the agent who handled it;
            // it settles below, so it no longer counts against their capacity.
            interaction.AgentId = transferringAgentId;
        }

        InteractionTransferHistory.CompletePending(interaction, now, InteractionTransferHistory.HandedOverAfterConsult, consult.TargetId);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        await _publisher.PublishAsync(
            TransferEventFactory.Transferred(interaction, transferringAgentId, command.UserId, InteractionTransferType.Consultative, consult.TargetType, consult.TargetId, InteractionTransferHistory.HandedOverAfterConsult, now),
            cancellationToken);

        // Committed before the consulting agent's leg is hung up: its hangup comes back as a webhook, and one that
        // found the leg still carrying the call would end the call and hang up the caller.
        await _session.SaveChangesAsync(cancellationToken);

        if (!toAgent)
        {
            // The caller now talks to somebody outside the contact center, so the call leaves it the way a blind
            // transfer out does: settled as transferred through provider truth.
            await _providerVoiceEventService.IngestAsync(new ProviderVoiceEvent
            {
                ProviderName = interaction.ProviderName,
                ProviderCallId = session.ProviderCallId,
                State = VoiceCallState.Transferred,
                OccurredUtc = now,
                IdempotencyKey = $"consult-completed:{consult.ConsultId}",
            }, cancellationToken);
        }

        await _agentRelease.HangUpAsync(interaction.ProviderName, agentLegs, cancellationToken);

        consult.Status = ConsultCallStatus.Completed;

        return WarmTransferResult.For(consult, "The call was handed over.");
    }

    /// <inheritdoc />
    public async Task<WarmTransferResult> CancelAsync(WarmTransferCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (interaction, authorization, failure) = await AuthorizeAsync(command.InteractionId, command.UserId, command.Principal, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        var consult = FindConsult(authorization.CallSession, authorization.AgentId, command.ConsultId);

        if (consult is null)
        {
            return WarmTransferResult.Failure("There is no consult on this call to cancel.");
        }

        var wasConnected = consult.Status == ConsultCallStatus.Connected;

        if (!await _consults.CancelAsync(authorization.CallSession.ItemId, consult.ConsultId, cancellationToken))
        {
            return WarmTransferResult.Failure("The consult could not be cancelled.");
        }

        InteractionTransferHistory.AbandonPending(interaction, InteractionTransferHistory.ConsultCancelled);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        if (wasConnected && consult.TargetType == InteractionTransferTargetType.Agent)
        {
            // Busy for the consult only; with it over they are ready for their next call.
            await _presenceManager.CompleteWorkAsync(consult.TargetId, new AgentStateChangeContext { InteractionId = interaction.ItemId, ChangedUtc = _clock.UtcNow }, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        consult.Status = ConsultCallStatus.Cancelled;

        return WarmTransferResult.For(consult, "The consult was cancelled. The caller is back with you.");
    }

    /// <inheritdoc />
    public async Task<WarmTransferResult> GetAsync(WarmTransferCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, authorization, failure) = await AuthorizeAsync(command.InteractionId, command.UserId, command.Principal, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        var consult = FindConsult(authorization.CallSession, authorization.AgentId, command.ConsultId);

        return consult is null
            ? WarmTransferResult.Failure("There is no consult on this call.")
            : WarmTransferResult.For(consult);
    }

    private async Task<(Interaction Interaction, CallControlAuthorizationResult Authorization, WarmTransferResult Failure)> AuthorizeAsync(
        string interactionId,
        string userId,
        System.Security.Claims.ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId) || string.IsNullOrEmpty(userId))
        {
            return (null, null, WarmTransferResult.Failure("The requested call is not available."));
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return (null, null, WarmTransferResult.Failure("The requested call is not available."));
        }

        var authorization = await _authorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Principal = principal,
            UserId = userId,
            Verb = CallControlVerb.Transfer,
            InteractionId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
        }, cancellationToken);

        if (!authorization.Succeeded || authorization.CallSession is null)
        {
            return (null, null, WarmTransferResult.Failure(authorization.FailureReason ?? "The requested call is not available."));
        }

        return (interaction, authorization, null);
    }

    // The agent's own consult: the one they asked about, or their most recent. A consult somebody else started is
    // not theirs to complete or cancel.
    private static ConsultCall FindConsult(CallSession session, string agentId, string consultId)
        => session.Consults
            .Where(consult =>
                consult is not null &&
                (string.IsNullOrEmpty(consult.InitiatedByAgentId) || string.Equals(consult.InitiatedByAgentId, agentId, StringComparison.Ordinal)) &&
                (string.IsNullOrEmpty(consultId) || string.Equals(consult.ConsultId, consultId, StringComparison.Ordinal)))
            .OrderBy(consult => consult.StartedUtc)
            .LastOrDefault();

    private async Task<string> ResolveHoldAudioAsync(string queueId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueId) || !ContactCenterConstants.QueueStartsAfterCallWork(queueId))
        {
            return null;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var mediaId = queue?.Treatment?.HoldMusicMediaId;

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return null;
        }

        // A URL plays as it is. A catalog id means nothing to the provider: the clip is held under the reference the
        // provider gave it at upload, and handing over the id instead plays silence.
        if (Uri.TryCreate(mediaId, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return mediaId;
        }

        var item = await _mediaItemManager.FindByIdAsync(mediaId, cancellationToken);

        return string.IsNullOrWhiteSpace(item?.MediaReference) ? null : item.MediaReference;
    }

    private static string DisplayName(AgentProfile agent)
        => !string.IsNullOrWhiteSpace(agent.DisplayName)
            ? agent.DisplayName
            : !string.IsNullOrWhiteSpace(agent.UserName) ? agent.UserName : agent.Name;
}
