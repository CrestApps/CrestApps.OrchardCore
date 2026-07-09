using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
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
    private readonly IAgentProfileManager _agentManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ITelephonyProviderResolver _telephonyProviderResolver;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterCallCommandService"/> class.
    /// </summary>
    /// <param name="reservationService">The reservation service used to accept or reject the offer.</param>
    /// <param name="reservationManager">The reservation manager used to validate offer ownership before state changes.</param>
    /// <param name="interactionManager">The interaction manager used to advance the interaction.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to connect media.</param>
    /// <param name="callSessionManager">The call session manager used to project the voice session.</param>
    /// <param name="inboundVoiceService">The inbound voice service used to re-offer a declined call.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterCallCommandService(
        IActivityReservationService reservationService,
        IActivityReservationManager reservationManager,
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ITelephonyProviderResolver telephonyProviderResolver,
        ICallSessionManager callSessionManager,
        IInboundVoiceService inboundVoiceService,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ILogger<ContactCenterCallCommandService> logger)
    {
        _reservationService = reservationService;
        _reservationManager = reservationManager;
        _interactionManager = interactionManager;
        _agentManager = agentManager;
        _voiceProviderResolver = voiceProviderResolver;
        _telephonyProviderResolver = telephonyProviderResolver;
        _callSessionManager = callSessionManager;
        _inboundVoiceService = inboundVoiceService;
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
            return CallCommandResult.Failure("No interaction is linked to the offered activity.");
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var provider = _voiceProviderResolver.Get(interaction.ProviderName);
        var hasProvider = provider is not null;
        var deliveryModel = hasProvider
            ? provider.DeliveryModel
            : VoiceProviderDeliveryModel.ServerSideAcd;
        var requiresDeviceAnswer = hasProvider && deliveryModel == VoiceProviderDeliveryModel.AgentDeviceNative;

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
                    interaction.ProviderInteractionId,
                    reservation.AgentId,
                    connectResult.ErrorCode,
                    connectResult.ErrorMessage);

                await _reservationService.CancelAsync(reservation.ItemId, cancellationToken);

                if (!string.IsNullOrEmpty(reservation.QueueId))
                {
                    await _inboundVoiceService.OfferNextAsync(reservation.QueueId, cancellationToken);
                }

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
                        interaction.ProviderInteractionId,
                        reservation.AgentId,
                        answerResult.Error);

                    await _reservationService.CancelAsync(reservation.ItemId, cancellationToken);

                    if (!string.IsNullOrEmpty(reservation.QueueId))
                    {
                        await _inboundVoiceService.OfferNextAsync(reservation.QueueId, cancellationToken);
                    }

                    return CallCommandResult.Failure(answerResult.Error ?? "The provider could not answer the call.");
                }
            }
        }

        var now = _clock.UtcNow;

        interaction.Status = InteractionStatus.Connected;
        interaction.StartedUtc ??= now;
        interaction.AnsweredUtc = now;
        interaction.AgentId = reservation.AgentId;
        interaction.QueueId ??= reservation.QueueId;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var session = await EnsureConnectedSessionAsync(interaction, reservation, deliveryModel, now, cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.OfferAccepted, interaction.ItemId, reservation.AgentId, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.CallConnected, interaction.ItemId, reservation.AgentId, cancellationToken);

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

    private async Task<CallSession> EnsureConnectedSessionAsync(
        Interaction interaction,
        ActivityReservation reservation,
        VoiceProviderDeliveryModel deliveryModel,
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
            session.State = ContactCenterCallState.Connected;
            session.CreatedUtc = now;
            session.StartedUtc = now;
            session.AnsweredUtc = now;
            await _callSessionManager.CreateAsync(session, cancellationToken: cancellationToken);

            await PublishAsync(ContactCenterConstants.Events.CallSessionCreated, interaction.ItemId, reservation.AgentId, cancellationToken);

            return session;
        }

        session.State = ContactCenterCallState.Connected;
        session.AgentId = reservation.AgentId;
        session.StartedUtc ??= now;
        session.AnsweredUtc ??= now;
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
