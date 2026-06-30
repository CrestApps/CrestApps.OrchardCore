using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderVoiceEventService"/>.
/// </summary>
public sealed class ProviderVoiceEventService : IProviderVoiceEventService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceEventService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callSessionManager">The call session manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to bridge answered outbound calls.</param>
    /// <param name="eventStore">The interaction event store used to de-duplicate provider events.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderVoiceEventService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IInteractionEventStore eventStore,
        IContactCenterEventPublisher publisher,
        IClock clock,
        ILogger<ProviderVoiceEventService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _eventStore = eventStore;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CallSession> IngestAsync(ProviderVoiceEvent providerEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);

        if (string.IsNullOrEmpty(providerEvent.ProviderCallId))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(providerEvent.IdempotencyKey) &&
            await _eventStore.ExistsByIdempotencyKeyAsync(providerEvent.IdempotencyKey, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping duplicate provider voice event with idempotency key '{IdempotencyKey}'.",
                    providerEvent.IdempotencyKey);
            }

            return null;
        }

        var interaction = await _interactionManager.FindByProviderInteractionIdAsync(providerEvent.ProviderCallId, cancellationToken);

        if (interaction is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Received a provider voice event for call '{ProviderCallId}' that does not match any interaction.",
                    providerEvent.ProviderCallId);
            }

            return null;
        }

        var now = providerEvent.OccurredUtc ?? _clock.UtcNow;
        var session = await EnsureSessionAsync(interaction, providerEvent, now, cancellationToken);

        ApplyState(session, interaction, providerEvent.State, now);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        if (providerEvent.State == ContactCenterCallState.Connected)
        {
            await TryBridgeAnsweredOutboundAsync(session, interaction, cancellationToken);
        }

        await PublishAsync(ResolveEventType(providerEvent.State), interaction.ItemId, session.AgentId, providerEvent.IdempotencyKey, cancellationToken);

        return session;
    }

    private async Task<CallSession> EnsureSessionAsync(
        Interaction interaction,
        ProviderVoiceEvent providerEvent,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByProviderCallIdAsync(providerEvent.ProviderCallId, cancellationToken)
            ?? await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (session is not null)
        {
            return session;
        }

        session = await _callSessionManager.NewAsync(cancellationToken: cancellationToken);
        session.InteractionId = interaction.ItemId;
        session.ActivityItemId = interaction.ActivityItemId;
        session.ProviderName = interaction.ProviderName ?? providerEvent.ProviderName;
        session.ProviderCallId = providerEvent.ProviderCallId;
        session.Direction = interaction.Direction;
        session.AgentId = interaction.AgentId;
        session.QueueId = interaction.QueueId;
        session.FromAddress = providerEvent.FromAddress ?? interaction.CustomerAddress;
        session.ToAddress = providerEvent.ToAddress;
        session.State = providerEvent.State;
        session.CreatedUtc = now;
        await _callSessionManager.CreateAsync(session, cancellationToken: cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.CallSessionCreated, interaction.ItemId, session.AgentId, idempotencyKey: null, cancellationToken);

        return session;
    }

    private static void ApplyState(CallSession session, Interaction interaction, ContactCenterCallState state, DateTime now)
    {
        session.State = state;

        switch (state)
        {
            case ContactCenterCallState.Dialing:
            case ContactCenterCallState.Ringing:
                session.StartedUtc ??= now;
                break;
            case ContactCenterCallState.Connected:
                session.StartedUtc ??= now;
                session.AnsweredUtc ??= now;
                session.IsOnHold = false;
                break;
            case ContactCenterCallState.OnHold:
                session.IsOnHold = true;
                break;
            case ContactCenterCallState.Ending:
                break;
            case ContactCenterCallState.Ended:
            case ContactCenterCallState.Failed:
            case ContactCenterCallState.NoAnswer:
            case ContactCenterCallState.Rejected:
            case ContactCenterCallState.Canceled:
            case ContactCenterCallState.Transferred:
                session.EndedUtc ??= now;
                session.IsOnHold = false;

                if (session.AnsweredUtc.HasValue)
                {
                    session.TalkSeconds = Math.Max(0, (now - session.AnsweredUtc.Value).TotalSeconds - session.HoldSeconds);
                }

                break;
        }

        interaction.Status = MapInteractionStatus(state);

        switch (state)
        {
            case ContactCenterCallState.Connected:
                interaction.StartedUtc ??= now;
                interaction.AnsweredUtc ??= now;
                break;
            case ContactCenterCallState.Ended:
            case ContactCenterCallState.Failed:
            case ContactCenterCallState.NoAnswer:
            case ContactCenterCallState.Rejected:
            case ContactCenterCallState.Canceled:
            case ContactCenterCallState.Transferred:
                interaction.EndedUtc ??= now;
                break;
        }
    }

    private static InteractionStatus MapInteractionStatus(ContactCenterCallState state)
    {
        return state switch
        {
            ContactCenterCallState.Planned => InteractionStatus.Created,
            ContactCenterCallState.Dialing => InteractionStatus.Ringing,
            ContactCenterCallState.Ringing => InteractionStatus.Ringing,
            ContactCenterCallState.Connected => InteractionStatus.Connected,
            ContactCenterCallState.OnHold => InteractionStatus.Held,
            ContactCenterCallState.Ending => InteractionStatus.Connected,
            ContactCenterCallState.Transferred => InteractionStatus.Transferring,
            ContactCenterCallState.Ended => InteractionStatus.Ended,
            ContactCenterCallState.Failed => InteractionStatus.Failed,
            ContactCenterCallState.NoAnswer => InteractionStatus.Failed,
            ContactCenterCallState.Rejected => InteractionStatus.Failed,
            ContactCenterCallState.Canceled => InteractionStatus.Failed,
            _ => InteractionStatus.Created,
        };
    }

    private static string ResolveEventType(ContactCenterCallState state)
    {
        return state switch
        {
            ContactCenterCallState.Connected => ContactCenterConstants.Events.CallConnected,
            ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled => ContactCenterConstants.Events.CallEnded,
            _ => ContactCenterConstants.Events.CallSessionUpdated,
        };
    }

    private async Task TryBridgeAnsweredOutboundAsync(CallSession session, Interaction interaction, CancellationToken cancellationToken)
    {
        if (session.Direction != InteractionDirection.Outbound || string.IsNullOrEmpty(session.AgentId))
        {
            return;
        }

        var provider = _voiceProviderResolver.Get(session.ProviderName);

        if (provider is null ||
            provider.DeliveryModel != VoiceProviderDeliveryModel.ServerSideAcd ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.AgentConnect))
        {
            return;
        }

        var connectResult = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ActivityId = interaction.ActivityItemId,
            InteractionId = interaction.ItemId,
            ProviderCallId = session.ProviderCallId,
            AgentId = session.AgentId,
            QueueId = session.QueueId,
        }, cancellationToken);

        if (!connectResult.Succeeded)
        {
            _logger.LogError(
                "The Contact Center voice provider '{Provider}' could not bridge answered outbound call '{ProviderCallId}' to agent '{AgentId}': {ErrorCode} {ErrorMessage}.",
                provider.TechnicalName,
                session.ProviderCallId,
                session.AgentId,
                connectResult.ErrorCode,
                connectResult.ErrorMessage);
        }
    }

    private Task PublishAsync(string eventType, string interactionId, string actorId, string idempotencyKey, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interactionId,
            AggregateType = nameof(CallSession),
            AggregateId = interactionId,
            ActorId = actorId,
            SourceComponent = ContactCenterConstants.Components.CallSessions,
            IdempotencyKey = idempotencyKey,
        }, cancellationToken);
    }
}
