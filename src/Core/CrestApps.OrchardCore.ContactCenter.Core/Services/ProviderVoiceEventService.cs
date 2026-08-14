using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IProviderVoiceEventService"/>.
/// </summary>
public sealed class ProviderVoiceEventService : IProviderVoiceEventService
{
    private const int MaxIngestionAttempts = 3;

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
    private readonly ISession _session;
    private readonly IVoiceIngressGate _ingressGate;
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
    /// <param name="session">The YesSql session used to commit provider truth before releasing the ingestion lock.</param>
    /// <param name="ingressGate">The provider-neutral gate that serializes each provider call stream.</param>
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
        ISession session,
        IVoiceIngressGate ingressGate,
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
        _session = session;
        _ingressGate = ingressGate;
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

        try
        {
            return await IngestCoreAsync(providerEvent, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            // The retry starts from the event as it was handed in, because the adjustments the attempt made
            // are derived from it and re-deriving them is what makes the retry equivalent to the first try.
            return await RetryInFreshScopeAsync(providerEvent, cancellationToken);
        }
    }

    private async Task<CallSession> IngestCoreAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken)
    {
        // Canonicalize the provider identity before any interaction, call, or event key is built so that
        // provider-contributed aliases (for example "Default Asterisk") collapse to a single stable
        // identity ("Asterisk") instead of mutating the stored provider name.
        var canonicalProviderName = _providerIdentityResolver.Canonicalize(providerEvent.ProviderName);

        // Scope the provider-supplied idempotency key by the canonical provider so identical raw delivery
        // identifiers emitted by different providers (for example the same numeric id from Asterisk and
        // Dialpad) cannot collide in the shared interaction-event idempotency space. Non-provider domain
        // events are unaffected because this path only runs for normalized provider voice events.
        var legacyIdempotencyKey = providerEvent.IdempotencyKey;

        providerEvent = providerEvent with
        {
            ProviderName = canonicalProviderName,
            IdempotencyKey = ContactCenterClaimKeys.BuildProviderEventIdempotencyKey(
                canonicalProviderName,
                legacyIdempotencyKey),
        };

        // The gate is the single lock authority for the stream. When ingestion was reached through the
        // provider-neutral fan-out the lease is already held, and the gate satisfies this request
        // re-entrantly instead of taking a second lock on the same call.
        await using var acquiredLock = await _ingressGate.AcquireAsync(
            providerEvent.ProviderName,
            providerEvent.ProviderCallId,
            cancellationToken);

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
                    providerEvent.ProviderCallId.SanitizeLogValue());
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
                providerEvent.ProviderCallId.SanitizeLogValue(),
                providerEvent.ProviderName,
                interaction.ProviderName);

            return null;
        }

        var providerNameCanonicalized = false;

        if (!string.IsNullOrWhiteSpace(providerEvent.ProviderName) &&
            !string.Equals(interaction.ProviderName, providerEvent.ProviderName, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Provider voice event for call '{ProviderCallId}' used provider '{ProviderName}', but the matching interaction was stored as '{StoredProviderName}'. Canonicalizing the interaction to the event provider.",
                providerEvent.ProviderCallId.SanitizeLogValue(),
                providerEvent.ProviderName,
                interaction.ProviderName);

            interaction.ProviderName = providerEvent.ProviderName;
            providerNameCanonicalized = true;
        }

        var duplicateEvent = !string.IsNullOrEmpty(providerEvent.IdempotencyKey) &&
            await _eventStore.ExistsByIdempotencyKeyAsync(providerEvent.IdempotencyKey, cancellationToken);

        if (!duplicateEvent &&
            !string.IsNullOrEmpty(legacyIdempotencyKey) &&
            !string.Equals(legacyIdempotencyKey, providerEvent.IdempotencyKey, StringComparison.Ordinal))
        {
            var interactionEvents = await _eventStore.GetByInteractionAsync(interaction.ItemId, cancellationToken);
            duplicateEvent = interactionEvents?.Any(interactionEvent =>
                string.Equals(interactionEvent.IdempotencyKey, legacyIdempotencyKey, StringComparison.Ordinal)) == true;
        }

        if (duplicateEvent)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping duplicate provider voice event with idempotency key '{IdempotencyKey}'.",
                    providerEvent.IdempotencyKey.SanitizeLogValue());
            }

            if (providerNameCanonicalized)
            {
                await _session.SaveChangesAsync(cancellationToken);
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

        if (session is null)
        {
            _logger.LogWarning(
                "Refused provider voice event for call '{ProviderCallId}' because the interaction-matched session is already bound to a different call.",
                providerEvent.ProviderCallId.SanitizeLogValue());

            await _session.SaveChangesAsync(cancellationToken);

            return null;
        }

        if (ShouldIgnoreEvent(session, providerEvent, now))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Ignored stale provider voice event '{IdempotencyKey}' for call '{ProviderCallId}'. Current state: {CurrentState}; incoming state: {IncomingState}; last provider event: {LastProviderEventUtc}; incoming event: {OccurredUtc}.",
                    providerEvent.IdempotencyKey.SanitizeLogValue(),
                    providerEvent.ProviderCallId.SanitizeLogValue(),
                    session.State,
                    providerEvent.State,
                    session.LastProviderEventUtc,
                    now);
            }

            await _session.SaveChangesAsync(cancellationToken);

            return session;
        }

        var previousState = session.State;
        var previousIsMuted = session.IsMuted;
        var previousRecordingState = session.RecordingState;
        var previousIsConference = session.IsConference;
        var previousParticipantCount = session.ParticipantCount;

        ApplyState(session, interaction, providerEvent.State, now);
        ApplyProviderDetails(session, interaction, providerEvent, now);
        ApplyHangupCause(session, providerEvent);

        // The watermark is monotonic. Accepting a late terminal delivery must not rewind it, because a rewound
        // watermark would re-admit deliveries the machine has already decided are stale.
        session.LastProviderEventUtc = session.LastProviderEventUtc.HasValue && session.LastProviderEventUtc.Value > now
            ? session.LastProviderEventUtc.Value
            : now;

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

        if (providerEvent.State == VoiceCallState.Connected)
        {
            await StageAnsweredOutboundBridgeAsync(session, interaction, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        return session;
    }

    private async Task<CallSession> RetryInFreshScopeAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 2; attempt <= MaxIngestionAttempts; attempt++)
        {
            CallSession session = null;

            try
            {
                await _scopeExecutor.ExecuteAsync<IProviderVoiceEventService>(async service =>
                {
                    session = await service.IngestAsync(providerEvent, cancellationToken);
                });

                return session;
            }
            catch (ConcurrencyException exception)
            {
                lastException = exception;
            }
        }

        throw lastException ?? new ConcurrencyException(new Document());
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
            if (string.IsNullOrWhiteSpace(session.ProviderCallId) &&
                !string.IsNullOrWhiteSpace(providerEvent.ProviderCallId))
            {
                session.ProviderCallId = providerEvent.ProviderCallId;

                if (string.IsNullOrWhiteSpace(session.ProviderName))
                {
                    session.ProviderName = providerEvent.ProviderName;
                }

                await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(session.ProviderCallId) &&
                     !string.IsNullOrWhiteSpace(providerEvent.ProviderCallId) &&
                     !string.Equals(session.ProviderCallId, providerEvent.ProviderCallId, StringComparison.Ordinal))
            {
                // The interaction-matched session is bound to a DIFFERENT call id.
                // Returning null causes IngestCoreAsync to refuse this event rather
                // than mis-applying it to the wrong session.
                return null;
            }

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
        session.MirrorProviderState(ResolveInitialSessionState(interaction));
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
        return VoiceStreamOrdering.ShouldDiscard(
            new VoiceStreamWatermark
            {
                Phase = GetLifecyclePhase(session.State),
                HighWaterSequence = session.HighWaterSequence,
                LastEventUtc = session.LastProviderEventUtc,
            },
            new VoiceStreamDelivery
            {
                Phase = GetLifecyclePhase(providerEvent.State),
                SequenceNumber = providerEvent.SequenceNumber,
                OccurredUtc = occurredUtc,
            });
    }

    private static VoiceCallLifecyclePhase GetLifecyclePhase(VoiceCallState state)
    {
        return state switch
        {
            VoiceCallState.Planned => VoiceCallLifecyclePhase.Planned,
            VoiceCallState.Dialing => VoiceCallLifecyclePhase.Alerting,
            VoiceCallState.Ringing => VoiceCallLifecyclePhase.Alerting,
            VoiceCallState.Connected => VoiceCallLifecyclePhase.Established,
            VoiceCallState.OnHold => VoiceCallLifecyclePhase.Established,
            VoiceCallState.Ending => VoiceCallLifecyclePhase.Ending,
            _ => VoiceCallLifecyclePhase.Terminal,
        };
    }

    private static void ApplyState(CallSession session, Interaction interaction, VoiceCallState state, DateTime now)
    {
        session.MirrorProviderState(state);
        session.IsMuted = state is VoiceCallState.Ended or
            VoiceCallState.Failed or
            VoiceCallState.NoAnswer or
            VoiceCallState.Rejected or
            VoiceCallState.Canceled or
            VoiceCallState.Transferred
            ? false
            : session.IsMuted;

        switch (state)
        {
            case VoiceCallState.Dialing:
            case VoiceCallState.Ringing:
                session.StartedUtc ??= now;
                break;
            case VoiceCallState.Connected:
                session.StartedUtc ??= now;
                session.AnsweredUtc ??= now;
                session.IsOnHold = false;
                break;
            case VoiceCallState.OnHold:
                session.IsOnHold = true;
                break;
            case VoiceCallState.Ending:
                break;
            case VoiceCallState.Ended:
            case VoiceCallState.Failed:
            case VoiceCallState.NoAnswer:
            case VoiceCallState.Rejected:
            case VoiceCallState.Canceled:
            case VoiceCallState.Transferred:
                session.EndedUtc ??= now;
                session.IsOnHold = false;

                if (session.AnsweredUtc.HasValue)
                {
                    session.TalkSeconds = Math.Max(0, (now - session.AnsweredUtc.Value).TotalSeconds - session.HoldSeconds);
                }

                break;
        }

        // The call session is the authority for a provider-backed call, and this projection keeps the interaction
        // reporting the same thing the session does. Ordering was already decided upstream, so re-deciding it
        // here with the interaction lifecycle table would let the two records disagree instead of agreeing.
        interaction.MirrorSessionStatus(MapInteractionStatus(state));

        switch (state)
        {
            case VoiceCallState.Connected:
                interaction.StartedUtc ??= now;
                interaction.AnsweredUtc ??= now;
                break;
            case VoiceCallState.Ended:
            case VoiceCallState.Failed:
            case VoiceCallState.NoAnswer:
            case VoiceCallState.Rejected:
            case VoiceCallState.Canceled:
            case VoiceCallState.Transferred:
                interaction.EndedUtc ??= now;
                break;
        }
    }

    private static void ApplyProviderDetails(CallSession session, Interaction interaction, ProviderVoiceEvent providerEvent, DateTime now)
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

        // A terminal call is normalized as not muted by ApplyState, which runs first. Re-applying a provider
        // mute flag here would contradict that decision and leave an ended call flagged as muted, so the flag
        // is honored only while the call is still live.
        if (providerEvent.IsMuted.HasValue && !IsTerminalState(session.State))
        {
            session.IsMuted = providerEvent.IsMuted.Value;
        }

        if (providerEvent.RecordingState.HasValue)
        {
            var previousRecordingState = interaction.RecordingState;

            session.RecordingState = providerEvent.RecordingState.Value;
            interaction.RecordingState = providerEvent.RecordingState.Value;

            // Maintain the same secure-pause invariants the recording service enforces, so a provider-reported
            // pause is visible to the auto-resume guard (which selects on a non-null timestamp) and a provider
            // resume or stop never leaves a stale pause timestamp or justification behind. Stamp the pause time
            // only on the transition into paused: a provider that re-emits Paused on ordinary call updates must
            // not keep pushing the auto-resume deadline forward and let a pause outlive its configured window.
            if (providerEvent.RecordingState.Value == RecordingState.Paused)
            {
                if (previousRecordingState != RecordingState.Paused || interaction.RecordingPausedUtc is null)
                {
                    interaction.RecordingPausedUtc = now;
                }
            }
            else
            {
                interaction.RecordingPausedUtc = null;
                interaction.RecordingPauseReason = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.RecordingReference))
        {
            session.RecordingReference = providerEvent.RecordingReference;
            interaction.RecordingReference = providerEvent.RecordingReference;
        }

        ApplyTopology(session, providerEvent, now);

        if (providerEvent.Metadata.Count > 0)
        {
            foreach (var entry in providerEvent.Metadata)
            {
                session.Metadata[entry.Key] = entry.Value;
            }
        }

        if (providerEvent.AnswerClassification.HasValue)
        {
            var classificationValue = providerEvent.AnswerClassification.Value.ToString();

            session.Metadata[ContactCenterConstants.TelephonyMetadata.AnswerClassification] = classificationValue;
            interaction.TechnicalMetadata[ContactCenterConstants.TelephonyMetadata.AnswerClassification] = classificationValue;
        }
    }

    private static void ApplyTopology(CallSession session, ProviderVoiceEvent providerEvent, DateTime now)
    {
        CallTopologyProjector.ApplyReportedParticipation(
            session,
            providerEvent.IsConference,
            providerEvent.ParticipantCount,
            now);

        // Providers that publish per-leg events name the leg; providers that publish per-call events do not,
        // and for those the call itself is the only leg the platform can honestly claim to have observed.
        var providerLegId = string.IsNullOrEmpty(providerEvent.ProviderLegId)
            ? providerEvent.ProviderCallId
            : providerEvent.ProviderLegId;

        if (string.IsNullOrEmpty(providerLegId))
        {
            return;
        }

        // The leg carried on the session's own call identifier is the party the contact center is serving.
        // Any other leg on the same session belongs to a party the platform did not originate, so its role is
        // left undetermined rather than guessed.
        var role = string.Equals(providerLegId, session.ProviderCallId, StringComparison.Ordinal)
            ? CallPartyRole.Customer
            : CallPartyRole.Unknown;

        var address = session.Direction == InteractionDirection.Inbound
            ? session.FromAddress
            : session.ToAddress;

        if (IsTerminalState(providerEvent.State))
        {
            // The session's own hangup cause is assigned after this projection runs, so the event's cause is
            // read directly rather than a field that is still null on the delivery that ends the call.
            CallTopologyProjector.EndLeg(session, providerLegId, now, providerEvent.HangupCause);

            // Every remaining leg ends with the call. A terminal session accepts no further deliveries, so a
            // leg left open here would stay open, and the bridge would keep claiming a party that has gone.
            CallTopologyProjector.EndRemainingLegs(session, now);

            // Stopping an engagement is refused once the call is terminal, so an engagement left live here
            // could never be closed by the supervisor who opened it and would report someone as listening to a
            // call that has ended.
            CallTopologyProjector.EndRemainingMonitorSessions(session, now);
            CallTopologyProjector.DestroyBridge(session, now);

            return;
        }

        var status = MapCallLegStatus(providerEvent.State);

        CallTopologyProjector.UpsertLeg(session, providerLegId, role, status, now, address);

        if (status is CallLegStatus.Answered or CallLegStatus.OnHold)
        {
            CallTopologyProjector.EnsureBridge(session, session.Bridge?.ProviderBridgeId, now);
            CallTopologyProjector.Join(session, providerLegId, role, now, address: address);
        }
    }

    private static CallLegStatus MapCallLegStatus(VoiceCallState state)
    {
        return state switch
        {
            VoiceCallState.Planned => CallLegStatus.Unknown,
            VoiceCallState.Dialing => CallLegStatus.Dialing,
            VoiceCallState.Ringing => CallLegStatus.Ringing,
            VoiceCallState.Connected => CallLegStatus.Answered,
            VoiceCallState.OnHold => CallLegStatus.OnHold,
            VoiceCallState.Ending => CallLegStatus.Answered,
            _ => CallLegStatus.Unknown,
        };
    }

    private static InteractionStatus MapInteractionStatus(VoiceCallState state)
    {
        return state switch
        {
            VoiceCallState.Planned => InteractionStatus.Created,
            VoiceCallState.Dialing => InteractionStatus.Ringing,
            VoiceCallState.Ringing => InteractionStatus.Ringing,
            VoiceCallState.Connected => InteractionStatus.Connected,
            VoiceCallState.OnHold => InteractionStatus.Held,
            VoiceCallState.Ending => InteractionStatus.Connected,
            VoiceCallState.Transferred => InteractionStatus.Transferring,
            VoiceCallState.Ended => InteractionStatus.Ended,
            VoiceCallState.Failed => InteractionStatus.Failed,
            VoiceCallState.NoAnswer => InteractionStatus.Failed,
            VoiceCallState.Rejected => InteractionStatus.Failed,
            VoiceCallState.Canceled => InteractionStatus.Failed,
            _ => InteractionStatus.Created,
        };
    }

    private static VoiceCallState ResolveInitialSessionState(Interaction interaction)
    {
        return interaction.Status switch
        {
            InteractionStatus.Connected => VoiceCallState.Connected,
            InteractionStatus.Held => VoiceCallState.OnHold,
            InteractionStatus.Transferring => VoiceCallState.Connected,
            InteractionStatus.Conferenced => VoiceCallState.Connected,
            _ => VoiceCallState.Ringing,
        };
    }

    private static List<string> ResolveEventTypes(
        VoiceCallState previousState,
        VoiceCallState currentState,
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

        if (currentState == VoiceCallState.Connected && previousState != VoiceCallState.Connected)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallConnected);
        }

        if (currentState == VoiceCallState.OnHold && previousState != VoiceCallState.OnHold)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallHeld);
        }

        if (previousState == VoiceCallState.OnHold && currentState == VoiceCallState.Connected)
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

        // Participation now changes as legs join and leave the bridge, not only when a provider publishes a
        // conference count. An ordinary two-party call gaining its customer and agent legs is not a conference
        // change, so the event stays scoped to calls that are, or have just stopped being, a conference.
        if (currentIsConference != previousIsConference ||
            ((currentIsConference || previousIsConference) && currentParticipantCount != previousParticipantCount))
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

    private static void ApplyHangupCause(CallSession session, ProviderVoiceEvent providerEvent)
    {
        if (!IsTerminalState(session.State) ||
            session.HangupCause.HasValue)
        {
            return;
        }

        var hangupCause = providerEvent.HangupCause ?? InferHangupCause(session.State);

        // The provider owns the release cause, but only the session knows whether the call was ever
        // answered, and that is what separates a completed conversation from an abandoned one. A
        // provider reports the same normal release for both, so this one refinement belongs here.
        if (hangupCause == HangupCause.NormalClearing && !session.AnsweredUtc.HasValue)
        {
            hangupCause = HangupCause.Canceled;
        }
        else if (hangupCause == HangupCause.Canceled && session.AnsweredUtc.HasValue)
        {
            hangupCause = HangupCause.NormalClearing;
        }

        session.HangupCause = hangupCause;
    }

    // A provider that reports a terminal state without a release cause has still reported the outcome
    // through the state itself, so the cause is derived from it rather than left unset. No call may end
    // without a recorded cause, because an unrecorded one cannot be counted in compliance or abandon
    // reporting later.
    private static HangupCause InferHangupCause(VoiceCallState state)
    {
        return state switch
        {
            VoiceCallState.Ended => HangupCause.NormalClearing,
            VoiceCallState.Transferred => HangupCause.NormalClearing,
            VoiceCallState.NoAnswer => HangupCause.NoAnswer,
            VoiceCallState.Rejected => HangupCause.Rejected,
            VoiceCallState.Canceled => HangupCause.Canceled,
            _ => HangupCause.Failed,
        };
    }

    private static bool IsTerminalState(VoiceCallState state)
    {
        return state is VoiceCallState.Ended or
            VoiceCallState.Failed or
            VoiceCallState.NoAnswer or
            VoiceCallState.Rejected or
            VoiceCallState.Canceled or
            VoiceCallState.Transferred;
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
