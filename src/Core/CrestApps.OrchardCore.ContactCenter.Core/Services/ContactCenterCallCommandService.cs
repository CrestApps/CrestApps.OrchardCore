using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterCallCommandService"/>. It performs
/// offer acceptance, media connection, and state advancement as one server-side transaction so the
/// orchestration state and the provider media state stay consistent.
/// </summary>
public sealed class ContactCenterCallCommandService : IContactCenterCallCommandService
{
    private readonly IActivityReservationService _reservationService;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IDialerProfileManager _dialerProfileManager;
    private readonly IDialerAttemptService _dialerAttemptService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ITelephonyProviderResolver _telephonyProviderResolver;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly IProviderCallStateSynchronizationService _providerCallStateSynchronizationService;
    private readonly IProviderVoiceOfferSynchronizationService _offerSynchronizationService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterCallCommandService"/> class.
    /// </summary>
    /// <param name="reservationService">The reservation service used to accept or reject the offer.</param>
    /// <param name="reservationManager">The reservation manager used to validate offer ownership before state changes.</param>
    /// <param name="interactionManager">The interaction manager used to advance the interaction.</param>
    /// <param name="activityManager">The activity manager used to resolve non-media work offers.</param>
    /// <param name="dialerProfileManagers">The optional dialer profile managers.</param>
    /// <param name="dialerAttemptServices">The optional dialer attempt services.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to connect media.</param>
    /// <param name="telephonyProviderResolver">The telephony provider resolver used for server-side answer operations.</param>
    /// <param name="callSessionManager">The call session manager used to project the voice session.</param>
    /// <param name="inboundVoiceService">The inbound voice service used to re-offer a declined call.</param>
    /// <param name="providerCallStateSynchronizationService">The provider call-state synchronization service.</param>
    /// <param name="offerSynchronizationService">The offer synchronization service used to release a queued call whose media can no longer be connected because it no longer exists on the provider.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterCallCommandService(
        IActivityReservationService reservationService,
        IActivityReservationManager reservationManager,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IEnumerable<IDialerProfileManager> dialerProfileManagers,
        IEnumerable<IDialerAttemptService> dialerAttemptServices,
        IAgentProfileManager agentManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ITelephonyProviderResolver telephonyProviderResolver,
        ICallSessionManager callSessionManager,
        IInboundVoiceService inboundVoiceService,
        IProviderCallStateSynchronizationService providerCallStateSynchronizationService,
        IProviderVoiceOfferSynchronizationService offerSynchronizationService,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ILogger<ContactCenterCallCommandService> logger)
    {
        _reservationService = reservationService;
        _reservationManager = reservationManager;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _dialerProfileManager = dialerProfileManagers.FirstOrDefault();
        _dialerAttemptService = dialerAttemptServices.FirstOrDefault();
        _agentManager = agentManager;
        _voiceProviderResolver = voiceProviderResolver;
        _telephonyProviderResolver = telephonyProviderResolver;
        _callSessionManager = callSessionManager;
        _inboundVoiceService = inboundVoiceService;
        _providerCallStateSynchronizationService = providerCallStateSynchronizationService;
        _offerSynchronizationService = offerSynchronizationService;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CallCommandResult> AcceptInboundOfferAsync(string reservationId, string agentUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);
        ArgumentException.ThrowIfNullOrEmpty(agentUserId);

        var reservation = await FindAuthorizedPendingReservationAsync(reservationId, agentUserId, cancellationToken);

        if (reservation is null)
        {
            return CallCommandResult.Failure("The offer is no longer available.");
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        if (interaction is null)
        {
            var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

            if (activity?.Source == ActivitySources.PreviewDial &&
                _dialerProfileManager is not null &&
                _dialerAttemptService is not null &&
                !string.IsNullOrWhiteSpace(activity.CampaignId))
            {
                var profile = await _dialerProfileManager.FindByCampaignAsync(activity.CampaignId, cancellationToken);

                if (profile is not null)
                {
                    return await _dialerAttemptService.TryDialAsync(profile, reservation, cancellationToken)
                        ? CallCommandResult.Success("The outbound call was started.", requiresDeviceAnswer: false)
                        : CallCommandResult.Failure("The outbound call could not be started.");
                }
            }

            reservation = await _reservationService.AcceptAsync(reservationId, cancellationToken);

            return reservation is null
                ? CallCommandResult.Failure("The offer is no longer available.")
                : CallCommandResult.Success("The work was accepted.", requiresDeviceAnswer: false);
        }

        interaction = await _providerCallStateSynchronizationService.RefreshInteractionAsync(interaction, cancellationToken);

        if (interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed)
        {
            return CallCommandResult.Failure("The offer is no longer available.");
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var provider = _voiceProviderResolver.Get(interaction.ProviderName);
        var hasProvider = provider is not null;
        var deliveryModel = hasProvider
            ? provider.DeliveryModel
            : VoiceProviderDeliveryModel.ServerSideAcd;
        var requiresDeviceAnswer = hasProvider &&
            deliveryModel == VoiceProviderDeliveryModel.AgentDeviceNative &&
            interaction.Status != InteractionStatus.Connected;

        reservation = await _reservationService.AcceptAsync(reservationId, cancellationToken);

        if (reservation is null)
        {
            return CallCommandResult.Failure("The offer is no longer available.");
        }

        if (deliveryModel == VoiceProviderDeliveryModel.ServerSideAcd &&
            provider is not null &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.AgentConnect))
        {
            var connectResult = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
            {
                ActivityId = reservation.ActivityItemId,
                InteractionId = interaction.ItemId,
                ProviderCallId = interaction.ProviderInteractionId,
                AgentId = reservation.AgentId,
                AgentUserId = agent?.UserId,
                QueueId = reservation.QueueId,
            }, cancellationToken);

            if (!connectResult.Succeeded)
            {
                _logger.LogError(
                    "The Contact Center voice provider '{Provider}' could not connect call '{ProviderCallId}' to agent '{AgentId}': {ErrorCode} {ErrorMessage}.",
                    provider.TechnicalName,
                    OperationalLogRedactor.Pseudonymize(interaction.ProviderInteractionId, OperationalLogIdentifierCategory.Call),
                    OperationalLogRedactor.Pseudonymize(reservation.AgentId, OperationalLogIdentifierCategory.Agent),
                    OperationalLogRedactor.Redact(connectResult.ErrorCode, OperationalLogFieldKind.FreeText),
                    OperationalLogRedactor.Redact(connectResult.ErrorMessage, OperationalLogFieldKind.FreeText));

                await HandleMediaConnectFailureAsync(reservation, interaction, cancellationToken);

                return CallCommandResult.Failure(connectResult.ErrorMessage ?? "The provider could not connect the call to the agent.");
            }
        }
        else if (deliveryModel == VoiceProviderDeliveryModel.ServerSideAcd)
        {
            var telephonyProvider = await _telephonyProviderResolver.GetAsync(interaction.ProviderName);

            if (telephonyProvider is not null)
            {
                var answerResult = await telephonyProvider.AnswerAsync(new CallReference
                {
                    CallId = interaction.ProviderInteractionId,
                }, cancellationToken);

                if (!answerResult.Succeeded)
                {
                    _logger.LogError(
                        "The telephony provider '{Provider}' could not answer inbound Contact Center call '{ProviderCallId}' for agent '{AgentId}': {ErrorMessage}.",
                        interaction.ProviderName,
                        OperationalLogRedactor.Pseudonymize(interaction.ProviderInteractionId, OperationalLogIdentifierCategory.Call),
                        OperationalLogRedactor.Pseudonymize(reservation.AgentId, OperationalLogIdentifierCategory.Agent),
                        OperationalLogRedactor.Redact(answerResult.Error, OperationalLogFieldKind.FreeText));

                    await HandleMediaConnectFailureAsync(reservation, interaction, cancellationToken);

                    return CallCommandResult.Failure(answerResult.Error ?? "The provider could not answer the call.");
                }
            }
        }

        var now = _clock.UtcNow;

        interaction.AgentId = reservation.AgentId;
        interaction.QueueId ??= reservation.QueueId;

        if (deliveryModel == VoiceProviderDeliveryModel.AgentDeviceNative && interaction.Status != InteractionStatus.Connected)
        {
            interaction.Status = InteractionStatus.Ringing;
            interaction.StartedUtc ??= now;
        }
        else
        {
            interaction.Status = InteractionStatus.Connected;
            interaction.StartedUtc ??= now;
            interaction.AnsweredUtc ??= now;
        }

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var session = await EnsureSessionAsync(
            interaction,
            reservation,
            deliveryModel,
            interaction.Status == InteractionStatus.Connected
                ? ContactCenterCallState.Connected
                : ContactCenterCallState.Ringing,
            interaction.Status == InteractionStatus.Connected,
            now,
            cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.OfferAccepted, interaction.ItemId, reservation.AgentId, cancellationToken);

        if (deliveryModel != VoiceProviderDeliveryModel.AgentDeviceNative)
        {
            await PublishAsync(ContactCenterConstants.Events.CallConnected, interaction.ItemId, reservation.AgentId, cancellationToken);
        }

        return new CallCommandResult
        {
            Succeeded = true,
            Reason = "The offer was accepted.",
            RequiresDeviceAnswer = requiresDeviceAnswer,
            InteractionId = interaction.ItemId,
            CallSessionId = session.ItemId,
        };
    }

    /// <inheritdoc/>
    public async Task<CallCommandResult> DeclineInboundOfferAsync(string reservationId, string agentUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);
        ArgumentException.ThrowIfNullOrEmpty(agentUserId);

        var reservation = await FindAuthorizedPendingReservationAsync(reservationId, agentUserId, cancellationToken);

        if (reservation is null)
        {
            return CallCommandResult.Failure("The offer is no longer available.");
        }

        reservation = await _reservationService.RejectAsync(reservationId, cancellationToken);

        if (reservation is null)
        {
            return CallCommandResult.Failure("The offer is no longer available.");
        }

        await PublishAsync(ContactCenterConstants.Events.OfferDeclined, null, reservation.AgentId, cancellationToken);

        if (!string.IsNullOrEmpty(reservation.QueueId))
        {
            await _inboundVoiceService.OfferNextAsync(reservation.QueueId, cancellationToken);
        }

        return CallCommandResult.Success("The offer was declined and re-offered.", requiresDeviceAnswer: false);
    }

    private async Task HandleMediaConnectFailureAsync(
        ActivityReservation reservation,
        Interaction interaction,
        CancellationToken cancellationToken)
    {
        // The provider could not connect or answer the accepted call. Ask the provider for the authoritative
        // call state: if the call no longer exists, remove it from the queue and release both the reservation
        // and the agent so the dead call is never re-offered and the agent can receive new calls. Only when
        // the call still exists on the provider do we treat this as a transient failure and re-offer it.
        var refreshed = await _providerCallStateSynchronizationService.RefreshInteractionAsync(interaction, cancellationToken);

        if (refreshed.Status is InteractionStatus.Ended or InteractionStatus.Failed)
        {
            await _offerSynchronizationService.ReconcileEndedOfferAsync(refreshed.ItemId, cancellationToken);
        }
        else
        {
            await _reservationService.CancelAsync(reservation.ItemId, cancellationToken);
        }

        if (!string.IsNullOrEmpty(reservation.QueueId))
        {
            await _inboundVoiceService.OfferNextAsync(reservation.QueueId, cancellationToken);
        }
    }

    private async Task<ActivityReservation> FindAuthorizedPendingReservationAsync(
        string reservationId,
        string agentUserId,
        CancellationToken cancellationToken)
    {
        var agent = await _agentManager.FindByUserIdAsync(agentUserId, cancellationToken);

        if (agent is null)
        {
            return null;
        }

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null ||
            reservation.Status != ReservationStatus.Pending ||
            !string.Equals(reservation.AgentId, agent.ItemId, StringComparison.Ordinal))
        {
            return null;
        }

        return reservation;
    }

    private async Task<CallSession> EnsureSessionAsync(
        Interaction interaction,
        ActivityReservation reservation,
        VoiceProviderDeliveryModel deliveryModel,
        ContactCenterCallState state,
        bool answered,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (session is null)
        {
            session = await _callSessionManager.NewAsync(cancellationToken: cancellationToken);
            session.InteractionId = interaction.ItemId;
            session.ActivityItemId = reservation.ActivityItemId;
            session.ProviderName = interaction.ProviderName;
            session.ProviderCallId = interaction.ProviderInteractionId;
            session.Direction = interaction.Direction;
            session.DeliveryModel = deliveryModel;
            session.FromAddress = interaction.CustomerAddress;
            session.QueueId = reservation.QueueId;
            session.AgentId = reservation.AgentId;
            session.State = state;
            session.CreatedUtc = now;
            session.StartedUtc = now;

            if (answered)
            {
                session.AnsweredUtc = now;
            }

            await _callSessionManager.CreateAsync(session, cancellationToken: cancellationToken);

            await PublishAsync(ContactCenterConstants.Events.CallSessionCreated, interaction.ItemId, reservation.AgentId, cancellationToken);

            return session;
        }

        session.State = state;
        session.AgentId = reservation.AgentId;
        session.StartedUtc ??= now;

        if (answered)
        {
            session.AnsweredUtc ??= now;
        }

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        return session;
    }

    private Task PublishAsync(string eventType, string interactionId, string actorId, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interactionId,
            AggregateType = nameof(Interaction),
            AggregateId = interactionId,
            ActorId = actorId,
            SourceComponent = ContactCenterConstants.Components.Voice,
        }, cancellationToken);
    }
}
