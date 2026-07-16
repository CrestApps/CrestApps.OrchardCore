using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using OrchardCore;
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
    private readonly ITelephonyProviderResolver _telephonyProviderResolver;
    private readonly IInteractionEventStore _eventStore;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IProviderIdentityResolver _providerIdentityResolver;
    private readonly IProviderCommandStateService _providerCommandStateService;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceEventService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callSessionManager">The call session manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to bridge answered outbound calls.</param>
    /// <param name="telephonyProviderResolver">The telephony provider resolver used to protect provider-scoped call identities.</param>
    /// <param name="eventStore">The interaction event store used to de-duplicate provider events.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="presenceManager">The presence manager used to move agents into wrap-up after handled calls end.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases before keying.</param>
    /// <param name="providerCommandStateService">The service used to persist outbound bridge intent.</param>
    /// <param name="scopeExecutor">The executor used to wake provider-command processing after commit.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    /// <param name="logger">The logger instance.</param>
    public ProviderVoiceEventService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ITelephonyProviderResolver telephonyProviderResolver,
        IInteractionEventStore eventStore,
        IContactCenterEventPublisher publisher,
        IAgentPresenceManager presenceManager,
        IProviderIdentityResolver providerIdentityResolver,
        IProviderCommandStateService providerCommandStateService,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<ProviderVoiceEventService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _telephonyProviderResolver = telephonyProviderResolver;
        _eventStore = eventStore;
        _publisher = publisher;
        _presenceManager = presenceManager;
        _providerIdentityResolver = providerIdentityResolver;
        _providerCommandStateService = providerCommandStateService;
        _scopeExecutor = scopeExecutor;
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

        // Canonicalize the provider identity before any interaction, call, or event key is built so that
        // provider-contributed aliases (for example "Default Asterisk") collapse to a single stable
        // identity ("Asterisk") instead of mutating the stored provider name.
        providerEvent.ProviderName = _providerIdentityResolver.Canonicalize(providerEvent.ProviderName);

        // Scope the provider-supplied idempotency key by the canonical provider so identical raw delivery
        // identifiers emitted by different providers (for example the same numeric id from Asterisk and
        // DialPad) cannot collide in the shared interaction-event idempotency space. Non-provider domain
        // events are unaffected because this path only runs for normalized provider voice events.
        var legacyIdempotencyKey = providerEvent.IdempotencyKey;
        providerEvent.IdempotencyKey = ContactCenterClaimKeys.BuildProviderEventIdempotencyKey(
            providerEvent.ProviderName,
            legacyIdempotencyKey);

        Interaction interaction = null;
        var matchedByCallIdOnly = false;

        if (!string.IsNullOrWhiteSpace(providerEvent.ProviderName))
        {
            interaction = await _interactionManager.FindByProviderInteractionIdAsync(
                providerEvent.ProviderName,
                providerEvent.ProviderCallId,
                cancellationToken);
        }

        if (interaction is null)
        {
            interaction = await _interactionManager.FindByProviderInteractionIdAsync(providerEvent.ProviderCallId, cancellationToken);
            matchedByCallIdOnly = interaction is not null;
        }

        if (interaction is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Received a provider voice event for call '{ProviderCallId}' that does not match any interaction.",
                    OperationalLogRedactor.Pseudonymize(providerEvent.ProviderCallId, OperationalLogIdentifierCategory.Call));
            }

            return null;
        }

        if (matchedByCallIdOnly &&
            !string.IsNullOrWhiteSpace(providerEvent.ProviderName) &&
            !string.IsNullOrWhiteSpace(interaction.ProviderName) &&
            !string.Equals(interaction.ProviderName, providerEvent.ProviderName, StringComparison.Ordinal) &&
            (_voiceProviderResolver.Get(interaction.ProviderName) is not null ||
                await _telephonyProviderResolver.GetAsync(interaction.ProviderName) is not null))
        {
            _logger.LogWarning(
                "Ignored provider voice event for call '{ProviderCallId}' from provider '{ProviderName}' because the call id matched an interaction owned by active provider '{StoredProviderName}'.",
                OperationalLogRedactor.Pseudonymize(providerEvent.ProviderCallId, OperationalLogIdentifierCategory.Call),
                providerEvent.ProviderName,
                interaction.ProviderName);

            return null;
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.ProviderName) &&
            !string.Equals(interaction.ProviderName, providerEvent.ProviderName, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Provider voice event for call '{ProviderCallId}' used provider '{ProviderName}', but the matching interaction was stored as '{StoredProviderName}'. Canonicalizing the interaction to the event provider.",
                OperationalLogRedactor.Pseudonymize(providerEvent.ProviderCallId, OperationalLogIdentifierCategory.Call),
                providerEvent.ProviderName,
                interaction.ProviderName);

            interaction.ProviderName = providerEvent.ProviderName;
        }

        var duplicateEvent = !string.IsNullOrEmpty(providerEvent.IdempotencyKey) &&
            await _eventStore.ExistsByIdempotencyKeyAsync(providerEvent.IdempotencyKey, cancellationToken);

        if (!duplicateEvent &&
            !string.IsNullOrEmpty(legacyIdempotencyKey) &&
            !string.Equals(legacyIdempotencyKey, providerEvent.IdempotencyKey, StringComparison.Ordinal))
        {
            var interactionEvents = await _eventStore.ListByInteractionAsync(interaction.ItemId, cancellationToken);
            duplicateEvent = interactionEvents?.Any(interactionEvent =>
                string.Equals(interactionEvent.IdempotencyKey, legacyIdempotencyKey, StringComparison.Ordinal)) == true;
        }

        if (duplicateEvent)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping duplicate provider voice event with idempotency key '{IdempotencyKey}'.",
                    OperationalLogRedactor.Pseudonymize(providerEvent.IdempotencyKey, OperationalLogIdentifierCategory.Event));
            }

            return (!string.IsNullOrWhiteSpace(providerEvent.ProviderName)
                ? await _callSessionManager.FindByProviderCallIdAsync(
                    providerEvent.ProviderName,
                    providerEvent.ProviderCallId,
                    cancellationToken)
                : await _callSessionManager.FindByProviderCallIdAsync(providerEvent.ProviderCallId, cancellationToken))
                ?? await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        }

        var now = providerEvent.OccurredUtc ?? _clock.UtcNow;
        var session = await EnsureSessionAsync(interaction, providerEvent, now, cancellationToken);

        if (ShouldIgnoreEvent(session, providerEvent, now))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Ignored stale provider voice event '{IdempotencyKey}' for call '{ProviderCallId}'. Current state: {CurrentState}; incoming state: {IncomingState}; last provider event: {LastProviderEventUtc}; incoming event: {OccurredUtc}.",
                    OperationalLogRedactor.Pseudonymize(providerEvent.IdempotencyKey, OperationalLogIdentifierCategory.Event),
                    OperationalLogRedactor.Pseudonymize(providerEvent.ProviderCallId, OperationalLogIdentifierCategory.Call),
                    session.State,
                    providerEvent.State,
                    session.LastProviderEventUtc,
                    now);
            }

            return session;
        }

        var previousState = session.State;
        var previousIsMuted = session.IsMuted;
        var previousRecordingState = session.RecordingState;
        var previousIsConference = session.IsConference;
        var previousParticipantCount = session.ParticipantCount;

        ApplyState(session, interaction, providerEvent.State, now);
        ApplyProviderDetails(session, interaction, providerEvent);
        session.LastProviderEventUtc = now;

        if (providerEvent.SequenceNumber.HasValue)
        {
            session.HighWaterSequence = session.HighWaterSequence.HasValue
                ? Math.Max(session.HighWaterSequence.Value, providerEvent.SequenceNumber.Value)
                : providerEvent.SequenceNumber.Value;
        }

        var startsWrapUp = IsTerminalState(providerEvent.State) &&
            !string.IsNullOrEmpty(session.AgentId) &&
            (session.AnsweredUtc.HasValue || interaction.AnsweredUtc.HasValue);

        if (startsWrapUp)
        {
            interaction.WrapUpStartedUtc ??= now;
        }

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        if (startsWrapUp)
        {
            await _presenceManager.StartWrapUpAsync(session.AgentId, cancellationToken);
        }

        foreach (var eventType in ResolveEventTypes(
            previousState,
            session.State,
            previousIsMuted,
            session.IsMuted,
            previousRecordingState,
            session.RecordingState,
            previousIsConference,
            session.IsConference,
            previousParticipantCount,
            session.ParticipantCount))
        {
            var idempotencyKey = ResolveEventIdempotencyKey(providerEvent.IdempotencyKey, eventType);

            await PublishAsync(eventType, interaction.ItemId, session.AgentId, idempotencyKey, cancellationToken);
        }

        if (providerEvent.State == ContactCenterCallState.Connected)
        {
            await StageAnsweredOutboundBridgeAsync(session, interaction, cancellationToken);
        }

        return session;
    }

    private async Task<CallSession> EnsureSessionAsync(
        Interaction interaction,
        ProviderVoiceEvent providerEvent,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var session = (!string.IsNullOrWhiteSpace(providerEvent.ProviderName)
            ? await _callSessionManager.FindByProviderCallIdAsync(
                providerEvent.ProviderName,
                providerEvent.ProviderCallId,
                cancellationToken)
            : await _callSessionManager.FindByProviderCallIdAsync(providerEvent.ProviderCallId, cancellationToken))
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

        // Seed the freshly created session with the interaction's pre-event state instead of the incoming
        // provider state. When the very first observed provider state is terminal (for example, a
        // reconciliation sweep that discovers the call no longer exists on the provider), this preserves a
        // real non-terminal -> terminal transition so the CallEnded event is still published and queue,
        // reservation, and agent cleanup runs. Without this seed the session would be created already
        // terminal, ResolveEventTypes would see no transition, and the offer would never be released.
        session.State = ResolveInitialSessionState(interaction);
        session.RecordingState = interaction.RecordingState;
        session.RecordingReference = interaction.RecordingReference;
        session.CreatedUtc = now;
        session.LastProviderEventUtc = now;
        await _callSessionManager.CreateAsync(session, cancellationToken: cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.CallSessionCreated, interaction.ItemId, session.AgentId, idempotencyKey: null, cancellationToken);

        return session;
    }

    private static bool ShouldIgnoreEvent(CallSession session, ProviderVoiceEvent providerEvent, DateTime occurredUtc)
    {
        // When the provider supplies a monotonic sequence, it is the authoritative order. Reject stale or
        // equal-order deliveries against the persisted high-water mark.
        if (providerEvent.SequenceNumber.HasValue && session.HighWaterSequence.HasValue)
        {
            if (providerEvent.SequenceNumber.Value <= session.HighWaterSequence.Value)
            {
                return true;
            }
        }
        else if (session.LastProviderEventUtc.HasValue)
        {
            if (occurredUtc < session.LastProviderEventUtc.Value)
            {
                return true;
            }

            // For providers that only supply timestamps, an equal-timestamp delivery must not regress the
            // normalized lifecycle (for example a late Ringing arriving after Connected).
            if (occurredUtc == session.LastProviderEventUtc.Value &&
                GetLifecycleRank(providerEvent.State) < GetLifecycleRank(session.State))
            {
                return true;
            }
        }

        return IsTerminalState(session.State);
    }

    private static int GetLifecycleRank(ContactCenterCallState state)
    {
        return state switch
        {
            ContactCenterCallState.Planned => 0,
            ContactCenterCallState.Dialing => 1,
            ContactCenterCallState.Ringing => 1,
            ContactCenterCallState.Connected => 2,
            ContactCenterCallState.OnHold => 2,
            ContactCenterCallState.Ending => 3,
            _ => 4,
        };
    }

    private static void ApplyState(CallSession session, Interaction interaction, ContactCenterCallState state, DateTime now)
    {
        session.State = state;
        session.IsMuted = state is ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled or
            ContactCenterCallState.Transferred
            ? false
            : session.IsMuted;

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

    private static void ApplyProviderDetails(CallSession session, Interaction interaction, ProviderVoiceEvent providerEvent)
    {
        if (!string.IsNullOrWhiteSpace(providerEvent.ProviderName))
        {
            session.ProviderName = providerEvent.ProviderName;
            interaction.ProviderName = providerEvent.ProviderName;
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.FromAddress))
        {
            session.FromAddress = providerEvent.FromAddress;
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.ToAddress))
        {
            session.ToAddress = providerEvent.ToAddress;
        }

        if (providerEvent.IsMuted.HasValue)
        {
            session.IsMuted = providerEvent.IsMuted.Value;
        }

        if (providerEvent.RecordingState.HasValue)
        {
            session.RecordingState = providerEvent.RecordingState.Value;
            interaction.RecordingState = providerEvent.RecordingState.Value;
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.RecordingReference))
        {
            session.RecordingReference = providerEvent.RecordingReference;
            interaction.RecordingReference = providerEvent.RecordingReference;
        }

        if (providerEvent.IsConference.HasValue)
        {
            session.IsConference = providerEvent.IsConference.Value;
        }

        if (providerEvent.ParticipantCount.HasValue)
        {
            session.ParticipantCount = Math.Max(0, providerEvent.ParticipantCount.Value);
        }

        if (providerEvent.Metadata.Count > 0)
        {
            foreach (var entry in providerEvent.Metadata)
            {
                session.Metadata[entry.Key] = entry.Value;
            }
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

    private static ContactCenterCallState ResolveInitialSessionState(Interaction interaction)
    {
        return interaction.Status switch
        {
            InteractionStatus.Connected => ContactCenterCallState.Connected,
            InteractionStatus.Held => ContactCenterCallState.OnHold,
            InteractionStatus.Transferring => ContactCenterCallState.Connected,
            InteractionStatus.Conferenced => ContactCenterCallState.Connected,
            _ => ContactCenterCallState.Ringing,
        };
    }

    private static List<string> ResolveEventTypes(
        ContactCenterCallState previousState,
        ContactCenterCallState currentState,
        bool previousIsMuted,
        bool currentIsMuted,
        RecordingState previousRecordingState,
        RecordingState currentRecordingState,
        bool previousIsConference,
        bool currentIsConference,
        int previousParticipantCount,
        int currentParticipantCount)
    {
        var eventTypes = new List<string>
        {
            ContactCenterConstants.Events.CallSessionUpdated,
        };

        if (currentState == ContactCenterCallState.Connected && previousState != ContactCenterCallState.Connected)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallConnected);
        }

        if (currentState == ContactCenterCallState.OnHold && previousState != ContactCenterCallState.OnHold)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallHeld);
        }

        if (previousState == ContactCenterCallState.OnHold && currentState == ContactCenterCallState.Connected)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallResumed);
        }

        if (currentIsMuted && !previousIsMuted)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallMuted);
        }

        if (!currentIsMuted && previousIsMuted)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallUnmuted);
        }

        if (currentRecordingState != previousRecordingState)
        {
            eventTypes.AddRange(ResolveRecordingEvents(previousRecordingState, currentRecordingState));
        }

        if (currentIsConference != previousIsConference || currentParticipantCount != previousParticipantCount)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallConferenceChanged);
        }

        if (IsTerminalState(currentState) && !IsTerminalState(previousState))
        {
            eventTypes.Add(ContactCenterConstants.Events.CallEnded);
        }

        return eventTypes;
    }

    private static string[] ResolveRecordingEvents(
        RecordingState previousState,
        RecordingState currentState)
    {
        if (currentState == previousState)
        {
            return [];
        }

        return currentState switch
        {
            RecordingState.Recording when previousState == RecordingState.Paused
                => [ContactCenterConstants.Events.RecordingResumed],
            RecordingState.Recording => [ContactCenterConstants.Events.RecordingStarted],
            RecordingState.Paused => [ContactCenterConstants.Events.RecordingPaused],
            RecordingState.Stopped => [ContactCenterConstants.Events.RecordingStopped],
            _ => [],
        };
    }

    private static bool IsTerminalState(ContactCenterCallState state)
    {
        return state is ContactCenterCallState.Ended or
            ContactCenterCallState.Failed or
            ContactCenterCallState.NoAnswer or
            ContactCenterCallState.Rejected or
            ContactCenterCallState.Canceled or
            ContactCenterCallState.Transferred;
    }

    private static string ResolveEventIdempotencyKey(string providerEventKey, string eventType)
    {
        if (string.IsNullOrEmpty(providerEventKey) ||
            eventType == ContactCenterConstants.Events.CallSessionUpdated)
        {
            return providerEventKey;
        }

        return ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(providerEventKey, eventType);
    }

    private async Task StageAnsweredOutboundBridgeAsync(
        CallSession session,
        Interaction interaction,
        CancellationToken cancellationToken)
    {
        if (session.Direction != InteractionDirection.Outbound || string.IsNullOrEmpty(session.AgentId))
        {
            return;
        }

        var provider = _voiceProviderResolver.Get(session.ProviderName);

        if (provider is null ||
            provider.DeliveryModel != VoiceProviderDeliveryModel.ServerSideAcd ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.AgentConnect) ||
            provider is not IContactCenterVoiceCallControlProvider)
        {
            return;
        }

        if (!session.Metadata.TryGetValue(ContactCenterConstants.CommandMetadata.CommandId, out var commandId) ||
            string.IsNullOrEmpty(commandId))
        {
            commandId = IdGenerator.GenerateId();
            session.Metadata[ContactCenterConstants.CommandMetadata.CommandId] = commandId;
            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        await _providerCommandStateService.RegisterAsync(new ProviderCommandRegistration
        {
            CommandId = commandId,
            ProviderName = session.ProviderName,
            CommandType = ProviderCommandType.Answer,
            ActivityItemId = interaction.ActivityItemId,
            InteractionId = interaction.ItemId,
            RemoveReservationFromQueueOnFailure = false,
            RequestPayload = JsonSerializer.Serialize(new ProviderAnswerCommandRequest
            {
                ActivityId = interaction.ActivityItemId,
                InteractionId = interaction.ItemId,
                ProviderCallId = session.ProviderCallId,
                AgentId = session.AgentId,
                QueueId = session.QueueId,
            }),
        }, cancellationToken);

        _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
            processor.DispatchAsync(commandId, CancellationToken.None));
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
