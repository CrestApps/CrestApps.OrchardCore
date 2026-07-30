using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Exposes Asterisk outbound dialing through the Contact Center voice provider boundary.
/// </summary>
internal sealed class AsteriskContactCenterVoiceProvider :
    IContactCenterVoiceProvider,
    IContactCenterVoiceCallControlProvider,
    IContactCenterVoiceRecordingProvider,
    IContactCenterVoiceMonitoringProvider,
    IContactCenterVoiceTransferProvider,
    IContactCenterVoiceConferenceProvider,
    IContactCenterVoiceAttendedTransferProvider
{
    private readonly ITelephonyProviderResolver _telephonyResolver;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IAsteriskAriClient _ariClient;
    private readonly IAsteriskChannelTenantBindingStore _channelTenantBindingStore;
    private readonly IAsteriskPjsipCredentialLeaseStore _pjsipCredentialLeaseStore;
    private readonly IAsteriskAgentChannelReadySignal _agentChannelReadySignal;
    private readonly IAsteriskRecordingIngestJobStore _recordingIngestJobStore;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskContactCenterVoiceProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskContactCenterVoiceProvider"/> class.
    /// </summary>
    /// <param name="telephonyResolver">The telephony provider resolver.</param>
    /// <param name="workManager">The feature work manager.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client.</param>
    /// <param name="channelTenantBindingStore">The tenant-scoped Asterisk channel binding store.</param>
    /// <param name="pjsipCredentialLeaseStore">The tenant-scoped store used to resolve an agent's live browser softphone endpoint.</param>
    /// <param name="agentChannelReadySignal">The tenant-scoped signal used to wait for an originated agent channel to enter Stasis.</param>
    /// <param name="recordingIngestJobStore">The tenant-scoped store used to durably queue completed recordings for secure ingestion.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskContactCenterVoiceProvider(
        ITelephonyProviderResolver telephonyResolver,
        IContactCenterFeatureWorkManager workManager,
        IAsteriskAriClient ariClient,
        IAsteriskChannelTenantBindingStore channelTenantBindingStore,
        IAsteriskPjsipCredentialLeaseStore pjsipCredentialLeaseStore,
        IAsteriskAgentChannelReadySignal agentChannelReadySignal,
        IAsteriskRecordingIngestJobStore recordingIngestJobStore,
        IClock clock,
        ILogger<AsteriskContactCenterVoiceProvider> logger,
        IStringLocalizer<AsteriskContactCenterVoiceProvider> stringLocalizer)
    {
        _telephonyResolver = telephonyResolver;
        _workManager = workManager;
        _ariClient = ariClient;
        _channelTenantBindingStore = channelTenantBindingStore;
        _pjsipCredentialLeaseStore = pjsipCredentialLeaseStore;
        _agentChannelReadySignal = agentChannelReadySignal;
        _recordingIngestJobStore = recordingIngestJobStore;
        _clock = clock;
        _logger = logger;
        Name = stringLocalizer["Asterisk"];
    }

    /// <inheritdoc/>
    public string TechnicalName => AsteriskConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public LocalizedString Name { get; }

    /// <inheritdoc/>
    public ContactCenterVoiceProviderCapabilities Capabilities
        => ContactCenterVoiceProviderCapabilities.DialerDial |
            ContactCenterVoiceProviderCapabilities.AgentConnect |
            ContactCenterVoiceProviderCapabilities.Recording |
            ContactCenterVoiceProviderCapabilities.Monitor |
            ContactCenterVoiceProviderCapabilities.Whisper |
            ContactCenterVoiceProviderCapabilities.Barge |
            ContactCenterVoiceProviderCapabilities.CallTransfer |
            ContactCenterVoiceProviderCapabilities.Conference;

    /// <inheritdoc/>
    public VoiceProviderDeliveryModel DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd;

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> DialAsync(
        ContactCenterDialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var providerName = AsteriskConstants.ProviderTechnicalName;
        var provider = await _telephonyResolver.GetAsync(providerName);

        if (provider is null)
        {
            providerName = AsteriskConstants.DefaultProviderTechnicalName;
            provider = await _telephonyResolver.GetAsync(providerName);
        }

        if (provider is null)
        {
            return Failure("provider_unavailable", "The Asterisk telephony provider is not configured.");
        }

        // Resolving a provider is not the same as it being able to place calls, so the dial contract is
        // required explicitly rather than assumed from the provider registration.
        if (!provider.Capabilities.HasFlag(TelephonyCapabilities.Dial) ||
            provider is not ITelephonyCallControlProvider callControlProvider)
        {
            return Failure("provider_unavailable", "The Asterisk telephony provider cannot place outbound calls.");
        }

        var result = await callControlProvider.DialAsync(new DialRequest
        {
            To = request.Destination,
            From = request.CallerId,
            Metadata = request.Metadata,
        }, cancellationToken);

        // Report the canonical Contact Center provider identity (TechnicalName) rather than the internal
        // telephony-provider resolution name. The realtime listener always emits events under TechnicalName,
        // so the interaction must be correlated under the same identity; the tenant-versus-default telephony
        // provider distinction is purely an internal resolution detail and must not leak into correlation.
        if (!result.Succeeded)
        {
            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = result.OutcomeUnknown,
                ErrorCode = result.OutcomeUnknown ? "dial_outcome_unknown" : "dial_failed",
                ErrorMessage = result.Error,
                ProviderName = TechnicalName,
            };
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = result.Call?.CallId,
            ProviderName = TechnicalName,
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> ConnectToAgentAsync(
        ContactCenterConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to connect the caller to the agent.");
        }

        // Fail closed when the per-acceptance provider command id is absent. A legitimate accept always carries it
        // (the connect command executor stamps it), and it is the fence that makes each connect attempt's ARI
        // resource ids unique (see CreateStableConnectKey). Proceeding without it would let the stable key fall back
        // to the reusable interaction id, reopening the ABA hazard where a late teardown tears down a re-offered
        // call's freshly created bridge. Rejecting here — before any ARI side effect — preserves that guarantee.
        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.CommandMetadata.CommandId, out var commandId) ||
            string.IsNullOrWhiteSpace(commandId))
        {
            return Failure("command_id_missing", "A provider command id is required to connect the caller to the agent.");
        }

        var agentEndpoint = await ResolveAgentEndpointAsync(request, cancellationToken);

        if (string.IsNullOrWhiteSpace(agentEndpoint))
        {
            return Failure("agent_endpoint_missing", "The selected agent has no live Asterisk softphone registration to connect the caller to.");
        }

        var callerChannelId = request.ProviderCallId.Trim();
        var stableKey = CreateStableConnectKey(request);
        var bridgeId = CreateDeterministicAriId(AsteriskAriConstants.AgentBridgePrefix, stableKey);
        var agentChannelId = CreateDeterministicAriId(AsteriskAriConstants.AgentChannelPrefix, stableKey);
        var bridgeCreateAttempted = false;
        var originateAttempted = false;
        var holdingDetached = false;
        var bindingPersisted = false;

        try
        {
            if (!await _ariClient.ChannelExistsAsync(callerChannelId, cancellationToken))
            {
                return Failure("caller_channel_not_found", "The Asterisk caller channel is no longer available.");
            }

            // Persist the caller-to-agent ownership binding durably BEFORE creating ANY ARI resource. Both the agent
            // channel and the mixing bridge use deterministic ids derived from the per-attempt command id, so the
            // binding is written up front with the exact ids the originate and bridge will use. That guarantees every
            // ARI resource this flow creates always has a pre-existing durable record to drive its cleanup:
            // compensation in this scope on a handled failure, or the reconciler's aged-Pending reclaim if the whole
            // process crashes mid-connect. It is written Pending so a terminal event during the bridging window tears
            // down only the half-built agent leg without hanging up the caller the connect flow still owns; it is
            // promoted to Connected once both legs are bridged, and removed by compensation on any failure. The store
            // commits in its own isolated tenant session, so the binding is visible to the realtime listener scope the
            // instant this returns.
            await _channelTenantBindingStore.CreateAsync(new AsteriskChannelTenantBinding
            {
                ChannelId = agentChannelId,
                ProviderName = TechnicalName,
                InteractionId = request.InteractionId,
                ProviderCallId = callerChannelId,
                BridgeId = bridgeId,
                PeerChannelId = callerChannelId,
                State = AsteriskChannelBindingState.Pending,
                CreatedUtc = _clock.UtcNow,
            });
            bindingPersisted = true;

            // Mark the create as ATTEMPTED before awaiting it. If Asterisk creates the bridge but the response is
            // lost (a dropped ack, or a crash between the server-side create and the await returning), the
            // deterministic bridge id can still be live, so the failure path must treat an attempted create as
            // possibly-orphaned and compensate it rather than skip its teardown.
            bridgeCreateAttempted = true;
            await _ariClient.CreateBridgeAsync(bridgeId, AsteriskAriConstants.MixingBridgeType, cancellationToken);

            var originateRequest = new AsteriskAriOriginateRequest
            {
                Endpoint = agentEndpoint,
                CallerId = callerChannelId,
                ChannelId = agentChannelId,
                AppArgs = [AsteriskConstants.OriginationMarkerVariableName, request.InteractionId ?? string.Empty, "agent"],
                Variables = new Dictionary<string, string>
                {
                    [AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue,
                    [AsteriskConstants.InteractionChannelVariableName] = request.InteractionId ?? string.Empty,
                },
            };

            // Register readiness before originating so the agent leg's StasisStart can never be missed between
            // the originate call returning and the wait beginning. The originate uses our deterministic channel
            // id, so the readiness key matches the id the StasisStart will carry.
            using var readyRegistration = _agentChannelReadySignal.Register(agentChannelId);

            originateAttempted = true;

            // The originate commits to our deterministic channel id (set on the request above), so the durable
            // binding written before this call already matches the live channel. The returned id is therefore not
            // substituted back — doing so could diverge the binding key from the live channel and strand the record.
            await _ariClient.OriginateAsync(originateRequest, cancellationToken);

            // An ARI originate returns as soon as the channel is dialing, not when the agent answers. The
            // channel can only be bridged once it has entered the Stasis application, so wait for its
            // owned-origination StasisStart (bounded by the answer timeout) before bridging the two legs. The
            // caller stays parked in the holding bridge until then, so it keeps hearing hold music instead of
            // dead air.
            var agentReady = await readyRegistration.WaitAsync(
                TimeSpan.FromSeconds(AsteriskAriConstants.AgentAnswerTimeoutSeconds),
                cancellationToken);

            if (!agentReady)
            {
                await CompensateAsync(
                    agentChannelId,
                    bridgeId,
                    callerChannelIdToReturn: null,
                    provisioningOutcomeAmbiguous: false,
                    CancellationToken.None);

                return Failure("agent_no_answer", "The selected agent did not answer before the caller-to-agent connect timed out.");
            }

            // Durably record that the caller is about to leave its holding bridge BEFORE the detach actually
            // happens, so a crash between detaching and finalizing cannot strand the caller: the reconciler reads
            // this marker from the Pending agent-leg binding and returns the still-alive caller to holding instead
            // of leaving it in silence with no bridge. A false result means a terminal event already claimed the
            // pending agent leg for teardown (or removed it), so that teardown — not this flow — now owns the
            // caller's disposition. This flow MUST NOT detach the caller in that case: the caller is still safely
            // parked in holding, and detaching it here then crashing before finalizing would strand it outside
            // every bridge with the durable record already owned (or gone) elsewhere. Abort instead and self-clean
            // only this attempt's own deterministic agent leg and mixing bridge; the caller stays parked for re-offer.
            var callerDetachMarked = await _channelTenantBindingStore.MarkCallerDetachedAsync(agentChannelId);

            if (!callerDetachMarked)
            {
                await CompensateAsync(
                    agentChannelId,
                    bridgeId,
                    callerChannelIdToReturn: null,
                    provisioningOutcomeAmbiguous: false,
                    CancellationToken.None);

                return Failure("agent_connect_lost", "The agent channel was torn down before the caller could be connected.");
            }

            await DetachFromHoldingBridgeAsync(callerChannelId, cancellationToken);
            holdingDetached = true;

            await _ariClient.AddChannelToBridgeAsync(bridgeId, callerChannelId, cancellationToken);
            await _ariClient.AddChannelToBridgeAsync(bridgeId, agentChannelId, cancellationToken);

            // Finalize the caller-to-agent connect with a single durable compare-and-set. MarkConnectedAsync
            // atomically promotes the still-pending agent leg to Connected using YesSql document-version optimistic
            // concurrency; that transition is the connect flow's half of the linearization with terminal-event
            // teardown, so no external lock is required — the durable state transition itself decides the winner. A
            // false result means a terminal event durably claimed the pending agent leg first (or it is otherwise
            // gone), so this attempt's agent channel and mixing bridge are forfeit.
            var connected = await _channelTenantBindingStore.MarkConnectedAsync(agentChannelId);

            if (!connected)
            {
                // Teardown won the race for the pending agent leg. Compensation claims the binding to coordinate: the
                // claim loses to the terminal-event teardown that already owns the durable record, so this path skips
                // the agent-leg and mixing-bridge cleanup (its owner performs it) and only reparks the caller so the
                // work is re-offered rather than stranded — a Pending-disposition teardown never releases the caller
                // the connect flow parked. Idempotent with the teardown (the ARI client treats already-gone resources
                // as success).
                await CompensateAsync(
                    agentChannelId,
                    bridgeId,
                    callerChannelId,
                    provisioningOutcomeAmbiguous: false,
                    CancellationToken.None);

                return Failure("agent_connect_lost", "The agent channel was torn down before the caller could be connected.");
            }

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderName = TechnicalName,
                ProviderCallId = callerChannelId,
                ProviderLegId = agentChannelId,
                Metadata = new Dictionary<string, string>
                {
                    [AsteriskAriConstants.AgentChannelMetadataKey] = agentChannelId,
                    ["bridgeId"] = bridgeId,
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to connect caller channel {CallerChannelId} to agent {AgentId}; compensating side effects.",
                OperationalLogRedactor.Pseudonymize(callerChannelId, OperationalLogIdentifierCategory.Call),
                OperationalLogRedactor.Pseudonymize(request.AgentId, OperationalLogIdentifierCategory.User));

            // When the failure was transport-ambiguous (a client timeout, a transport error that returned no server
            // response, or a server error), the bridge-create or originate may still commit on Asterisk after this
            // compensation runs, so a hang-up or bridge destroy that "succeeds" only because the resource is not there
            // yet must not delete the durable record. Retain it so the age-gated reconciler re-probes live ARI state
            // and reclaims a resource that materialized after the failure.
            var provisioningOutcomeAmbiguous = AsteriskAriOutcomeClassifier.IsProvisioningOutcomeAmbiguous(ex);

            await CompensateAsync(
                bindingPersisted ? agentChannelId : null,
                bridgeCreateAttempted ? bridgeId : null,
                holdingDetached ? callerChannelId : null,
                provisioningOutcomeAmbiguous,
                CancellationToken.None);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                ProviderName = TechnicalName,
                ProviderCallId = callerChannelId,
                ErrorCode = "agent_connect_failed",
                ErrorMessage = "The Asterisk caller-to-agent bridge could not be completed.",
                OutcomeUnknown = bridgeCreateAttempted || originateAttempted,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> SetRecordingStateAsync(
        ContactCenterVoiceRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to change the recording state.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to derive the Asterisk recording name.");
        }

        // Resolve the canonical conversation (mixing) bridge from a binding owned by THIS tenant's store. The bridge
        // persists across transfer and conference, so recording it keeps the whole conversation on one continuous
        // recording. Failing closed when no owning binding exists enforces CC-1: a supervisor can never record a
        // call this tenant does not own.
        var bridgeId = await ResolveConversationBridgeAsync(request.ProviderCallId);

        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return Failure("recording_call_not_owned", "No owned Asterisk conversation bridge was found for the requested recording.");
        }

        var recordingName = CreateRecordingName(request.InteractionId);

        try
        {
            return request.State switch
            {
                RecordingState.Recording => await StartOrResumeRecordingAsync(bridgeId, recordingName, cancellationToken),
                RecordingState.Paused => await PauseRecordingAsync(recordingName, cancellationToken),
                RecordingState.Stopped or RecordingState.None => await StopRecordingAsync(request.InteractionId, recordingName, cancellationToken),
                _ => Failure("recording_state_unsupported", "The requested recording state is not supported."),
            };
        }
        catch (AsteriskAriException ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to change the recording state to {RecordingState} for interaction {InteractionId}.",
                request.State,
                OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

            var outcomeUnknown = IsAmbiguousAriOutcome(ex);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = outcomeUnknown,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId,
                ErrorCode = outcomeUnknown ? "recording_outcome_unknown" : "recording_failed",
                ErrorMessage = "The Asterisk recording state change could not be confirmed.",
            };
        }
    }

    private async Task<ContactCenterVoiceProviderResult> StartOrResumeRecordingAsync(
        string bridgeId,
        string recordingName,
        CancellationToken cancellationToken)
    {
        var recording = await _ariClient.StartBridgeRecordingAsync(
            bridgeId,
            recordingName,
            AsteriskAriConstants.RecordingFormat,
            cancellationToken);

        // A start that reused an existing paused recording (the deterministic name was already in progress) must be
        // resumed so the request to record actually produces audio. A freshly created recording is already active.
        if (string.Equals(recording?.State, AsteriskAriConstants.RecordingPausedState, StringComparison.OrdinalIgnoreCase))
        {
            await _ariClient.UnpauseBridgeRecordingAsync(recordingName, cancellationToken);
        }

        var format = string.IsNullOrWhiteSpace(recording?.Format)
            ? AsteriskAriConstants.RecordingFormat
            : recording.Format;

        return RecordingSuccess(recordingName, format, durationSeconds: null);
    }

    private async Task<ContactCenterVoiceProviderResult> PauseRecordingAsync(
        string recordingName,
        CancellationToken cancellationToken)
    {
        await _ariClient.PauseBridgeRecordingAsync(recordingName, cancellationToken);

        return RecordingSuccess(recordingName, AsteriskAriConstants.RecordingFormat, durationSeconds: null);
    }

    private async Task<ContactCenterVoiceProviderResult> StopRecordingAsync(
        string interactionId,
        string recordingName,
        CancellationToken cancellationToken)
    {
        var stored = await _ariClient.StopBridgeRecordingAsync(recordingName, cancellationToken);
        var format = string.IsNullOrWhiteSpace(stored?.Format)
            ? AsteriskAriConstants.RecordingFormat
            : stored.Format;

        // Queue the completed recording for durable, encrypted ingestion. The recording has already stopped, so a
        // transient enqueue failure must not fail the stop; the idempotent durable job then owns download-and-store
        // with retry and dead-lettering.
        await EnqueueRecordingIngestAsync(interactionId, recordingName, format, cancellationToken);

        return RecordingSuccess(recordingName, format, stored?.Duration);
    }

    private async Task EnqueueRecordingIngestAsync(
        string interactionId,
        string recordingName,
        string format,
        CancellationToken cancellationToken)
    {
        try
        {
            await _recordingIngestJobStore.EnqueueAsync(interactionId, recordingName, format, _clock.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Failed to enqueue recording {RecordingName} for ingestion.",
                OperationalLogRedactor.Pseudonymize(recordingName, OperationalLogIdentifierCategory.Call));
        }
    }

    private static ContactCenterVoiceProviderResult RecordingSuccess(
        string recordingName,
        string format,
        int? durationSeconds)
    {
        var metadata = new Dictionary<string, string>
        {
            [ContactCenterConstants.RecordingMetadata.ProviderRecordingId] = recordingName,
            [ContactCenterConstants.RecordingMetadata.StorageReference] = recordingName,
            [ContactCenterConstants.RecordingMetadata.Format] = format,
            [ContactCenterConstants.RecordingMetadata.RetrievalPath] = AsteriskAriConstants.StoredRecordingRetrievalPathPrefix + recordingName,
        };

        if (durationSeconds.HasValue)
        {
            metadata[ContactCenterConstants.RecordingMetadata.DurationSeconds] =
                durationSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            Metadata = metadata,
        };
    }

    private async Task<string> ResolveConversationBridgeAsync(string providerCallId)
    {
        // The caller-to-agent connect writes the mixing bridge id onto the agent-leg binding whose PeerChannelId is
        // the caller channel, so a recording request that carries the caller channel id resolves the bridge through
        // that binding. Only bindings persisted in this tenant's store are considered, so ownership is structural.
        var peerBindings = await _channelTenantBindingStore.FindAllByPeerChannelIdAsync(providerCallId);

        var owning = peerBindings.FirstOrDefault(binding => !string.IsNullOrWhiteSpace(binding.BridgeId));

        if (owning is not null)
        {
            return owning.BridgeId;
        }

        // The request may instead carry an id that is itself a bound channel (for example the agent leg), so also try
        // a direct channel lookup before failing closed.
        var direct = await _channelTenantBindingStore.FindByChannelIdAsync(providerCallId);

        return string.IsNullOrWhiteSpace(direct?.BridgeId)
            ? null
            : direct.BridgeId;
    }

    private static string CreateRecordingName(string interactionId)
    {
        // The recording name is derived from the globally unique interaction id (a 26-character generated id), so it
        // is stable across pause/resume/stop and inherently distinct per tenant without an extra prefix lookup.
        return CreateDeterministicAriId(AsteriskAriConstants.RecordingNamePrefix, interactionId);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> TransferAsync(
        ContactCenterVoiceTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to transfer the call.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to transfer the call.");
        }

        // Only a blind transfer to an agent is executable on Asterisk today. Consultative (warm) transfer needs a
        // two-phase consult/complete contract the single-call provider boundary does not express, and external,
        // queue, and entry-point destinations need trunk origination or a re-route media path that is not yet built.
        // Reject those as CONFIRMED failures (never OutcomeUnknown) so the transfer service surfaces a clear,
        // non-ambiguous result instead of leaving the call in an unknown state.
        if (request.TransferType != InteractionTransferType.Blind)
        {
            return Failure("transfer_type_unsupported", "The Asterisk provider only supports blind transfers.");
        }

        if (request.TargetType != InteractionTransferTargetType.Agent)
        {
            return Failure("transfer_target_unsupported", "The Asterisk provider only supports transferring a call to an agent.");
        }

        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.TransferMetadata.AgentUserId, out var targetUserId) ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return Failure("transfer_target_missing", "A destination agent is required to transfer the call.");
        }

        var callerChannelId = request.ProviderCallId.Trim();

        // Resolve the canonical conversation bridge and the current agent leg from a binding owned by THIS tenant's
        // store BEFORE resolving anything about the destination. Failing closed here first enforces CC-1 — an agent
        // can never transfer a call this tenant does not own — and guarantees an unowned call always fails with the
        // single unambiguous ownership reason rather than leaking the destination's registration state.
        var currentLeg = await ResolveOwnedAgentLegAsync(callerChannelId);

        if (currentLeg is null)
        {
            return Failure("transfer_call_not_owned", "No owned Asterisk conversation bridge was found for the requested transfer.");
        }

        var bridgeId = currentLeg.BridgeId;
        var currentAgentChannelId = currentLeg.ChannelId;
        var newAgentChannelId = CreateDeterministicAriId(
            AsteriskAriConstants.TransferAgentChannelPrefix,
            string.Concat(request.InteractionId, "-", targetUserId));

        // A retried transfer to the same destination must be idempotent: if the current agent leg IS the leg this
        // transfer would create (its deterministic id), the transfer already completed, so re-originating would ring
        // the destination a second time. Confirm the completed transfer instead.
        if (string.Equals(newAgentChannelId, currentAgentChannelId, StringComparison.Ordinal))
        {
            return TransferSuccess(newAgentChannelId, bridgeId);
        }

        var destinationEndpoint = await ResolveLiveSoftphoneEndpointAsync(targetUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(destinationEndpoint))
        {
            return Failure("transfer_target_offline", "The destination agent has no live Asterisk softphone registration to transfer the call to.");
        }

        // Persist the transfer's exactly-once ownership claim on the deterministic destination channel id BEFORE
        // originating. The durable Joining binding is BOTH the per-conversation transfer claim — a concurrent
        // duplicate transfer to the same destination loses this serialized create and must not re-ring the
        // destination — AND the guarantee that ANY death of the destination leg (before, during, or after bridging
        // but before the handoff commits) is a teardown no-op that leaves the caller with the current agent, because
        // a Joining leg never owns the shared canonical bridge. The BridgeId is recorded so recording/monitoring and
        // the atomic swap can resolve the conversation, but it is not an ownership claim while the state is Joining.
        var claimedNewLeg = await _channelTenantBindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = newAgentChannelId,
            ProviderName = TechnicalName,
            InteractionId = request.InteractionId,
            ProviderCallId = callerChannelId,
            BridgeId = bridgeId,
            PeerChannelId = callerChannelId,
            State = AsteriskChannelBindingState.Joining,
            CreatedUtc = _clock.UtcNow,
        });

        if (!claimedNewLeg)
        {
            // The serialized durable create lost. Distinguish an ACTIVE concurrent duplicate transfer to the same
            // destination (an in-flight Joining claim — the handoff outcome is identical, so confirm success without
            // re-ringing the destination or disturbing the concurrent transfer's leg) from a STALE claim: a prior
            // attempt's destination leg whose terminal event left a Terminating recovery record for the reconciler to
            // reclaim. Reporting success for a stale claim would falsely confirm a transfer that has no live
            // destination leg while the previous agent still owns the call, so fail closed (confirmed, retryable)
            // instead and let the caller retry once the stale claim is retired.
            var existingClaim = await _channelTenantBindingStore.FindByChannelIdAsync(newAgentChannelId);

            if (existingClaim is not null && existingClaim.State == AsteriskChannelBindingState.Joining)
            {
                return TransferSuccess(newAgentChannelId, bridgeId);
            }

            return Failure("transfer_failed", "A previous transfer to this destination is still being reclaimed; retry the transfer shortly.");
        }

        var originateAttempted = false;
        var handoffCommitted = false;

        try
        {
            // Register readiness before originating so the destination leg's StasisStart can never be missed between
            // the originate call returning and the wait beginning. The originate uses our deterministic channel id,
            // so the readiness key matches the id the StasisStart will carry.
            using var readyRegistration = _agentChannelReadySignal.Register(newAgentChannelId);

            var originateRequest = new AsteriskAriOriginateRequest
            {
                Endpoint = destinationEndpoint,
                CallerId = callerChannelId,
                ChannelId = newAgentChannelId,
                AppArgs = [AsteriskConstants.OriginationMarkerVariableName, request.InteractionId, "agent"],
                Variables = new Dictionary<string, string>
                {
                    [AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue,
                    [AsteriskConstants.InteractionChannelVariableName] = request.InteractionId,
                },
            };

            // Mark the originate as attempted BEFORE issuing it: an ambiguous transport failure (no ARI response) may
            // still have created the destination channel under our deterministic id, so the compensation path must be
            // able to best-effort release it even though the originate never returned success.
            originateAttempted = true;

            await _ariClient.OriginateAsync(originateRequest, cancellationToken);

            // The destination can only be bridged once it has entered Stasis (that is, once the destination agent
            // answers), so wait for its owned-origination StasisStart bounded by the answer timeout. The customer
            // keeps talking to the current agent on the canonical bridge until the destination answers, so a blind
            // transfer to an agent who never answers leaves the ORIGINAL call fully intact.
            var destinationReady = await readyRegistration.WaitAsync(
                TimeSpan.FromSeconds(AsteriskAriConstants.AgentAnswerTimeoutSeconds),
                cancellationToken);

            if (!destinationReady)
            {
                // A cancelled wait surfaces as a not-ready result, so distinguish genuine no-answer from cancellation
                // and let cancellation propagate to the unknown-outcome path instead of reporting a confirmed timeout.
                cancellationToken.ThrowIfCancellationRequested();

                // The destination leg was originated and is ringing (it simply never answered), so its channel is
                // live. Best-effort hang it up; if that hangup is not confirmed, keep a durable recovery record so
                // the reconciler reclaims the dangling channel instead of leaking it.
                var noAnswerHungUp = await TryHangupAsync(newAgentChannelId, CancellationToken.None);
                await RetireProvisionalTransferClaimAsync(newAgentChannelId, noAnswerHungUp);

                return Failure("transfer_no_answer", "The destination agent did not answer before the transfer timed out.");
            }

            // Commit point: add the answered destination leg to the SAME canonical conversation bridge the customer is
            // already on, so recording stays on one continuous bridge and the customer never leaves the recorded
            // conversation. The destination binding is still Joining (non-owning), so a destination death anywhere in
            // this window is a teardown no-op that leaves the customer with the current agent. The handoff becomes
            // durable only when FinalizeTransferHandoffAsync atomically promotes it and retires the previous leg.
            await _ariClient.AddChannelToBridgeAsync(bridgeId, newAgentChannelId, cancellationToken);
            handoffCommitted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to transfer interaction {InteractionId}; compensating the new destination leg.",
                OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

            // Pre-commit compensation. Because the serialized durable Joining create above gave THIS transfer
            // exclusive ownership of the deterministic destination id (a concurrent duplicate lost the create and
            // never originated), releasing our own destination leg can never touch another transfer's live call — no
            // existence probe or winner check is needed. A Joining leg never owns the shared bridge, so compensating
            // in any order can never drop the customer, who stays with the current agent on the canonical bridge.
            if (!handoffCommitted)
            {
                // The destination channel may be live whenever the originate was attempted and Asterisk did not
                // DEFINITELY reject it: a received 4xx/5xx response (non-null status) proves no channel was created
                // under our id, but a transport-ambiguous or cancelled originate may still have created it. Hang up
                // the leg only when it may exist, and keep a durable recovery record whenever its disposition is
                // unconfirmed so the reconciler reclaims a leaked channel instead of hard-deleting the only record
                // that could find it.
                var channelDefinitelyRejected = ex is AsteriskAriException rejected && rejected.StatusCode is not null;
                var channelMayExist = originateAttempted && !channelDefinitelyRejected;
                var channelConfirmedGone = true;

                if (channelMayExist)
                {
                    channelConfirmedGone = await TryHangupAsync(newAgentChannelId, CancellationToken.None);
                }

                await RetireProvisionalTransferClaimAsync(newAgentChannelId, channelConfirmedGone);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            var outcomeUnknown = ex is AsteriskAriException ariException && IsAmbiguousAriOutcome(ariException);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = outcomeUnknown,
                ProviderName = TechnicalName,
                ProviderCallId = callerChannelId,
                ErrorCode = outcomeUnknown ? "transfer_outcome_unknown" : "transfer_failed",
                ErrorMessage = "The Asterisk call transfer could not be completed.",
            };
        }

        // Post-commit finalization: the destination leg is bridged with the customer, so the transfer has physically
        // succeeded and is reported as success. Ownership is transferred atomically and the previous leg retired on a
        // best-effort basis — never rolling back the new leg — leaving any residue to the terminal-event/reconciler
        // path, exactly as the connect flow tolerates post-commit ARI hiccups.
        await FinalizeTransferHandoffAsync(
            currentAgentChannelId,
            newAgentChannelId,
            request.InteractionId);

        return TransferSuccess(newAgentChannelId, bridgeId);
    }

    private async Task FinalizeTransferHandoffAsync(
        string currentAgentChannelId,
        string newAgentChannelId,
        string interactionId)
    {
        try
        {
            // Atomically promote the destination leg from Joining to Connected AND retire the previous agent leg in
            // ONE transaction, so ownership of the canonical conversation bridge transfers from the previous agent to
            // the destination agent with no instant of two Connected owners (a double-teardown drop) or zero owners
            // (an unowned live bridge). Until this commits the destination leg is Joining and non-owning, so the
            // customer is always owned by exactly one Connected binding — the previous agent before the swap, the
            // destination agent after it.
            var swapped = await _channelTenantBindingStore.SwapConnectedOwnerAsync(newAgentChannelId, currentAgentChannelId);

            if (!swapped)
            {
                // The destination leg was no longer Joining — a terminal event claimed it for teardown between the
                // bridge commit and this swap. The previous agent leg is deliberately left as the Connected owner, so
                // the customer safely keeps the previous agent and the dangling destination residue is reclaimed by
                // the reconciler. Never a drop, never a double owner.
                _logger.LogWarning(
                    "Asterisk transfer of interaction {InteractionId} could not finalize the handoff because the destination leg was already claimed for teardown; leaving the previous agent as the owner.",
                    OperationalLogRedactor.Pseudonymize(interactionId, OperationalLogIdentifierCategory.Call));

                return;
            }

            // The destination leg now solely owns the canonical bridge, and the previous leg's binding was atomically
            // retired to a NON-OWNING Terminating (Joining-disposition) recovery record, so hanging up the previous
            // leg cannot tear down the conversation — its terminal event finds no owning binding and only releases its
            // own channel. Best-effort hang up the previous channel; when that hangup is confirmed remove its now
            // redundant recovery record, and when it is unconfirmed leave the durable record so the reconciler
            // reclaims the possibly-live previous channel instead of leaking it. This is the same post-commit,
            // update-then-call-ARI residual the connect flow tolerates.
            var previousHungUp = await TryHangupAsync(currentAgentChannelId, CancellationToken.None);
            await RetireProvisionalTransferClaimAsync(currentAgentChannelId, previousHungUp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk transfer of interaction {InteractionId} bridged the destination agent but did not finalize the previous leg cleanly; leaving residue for the reconciler.",
                OperationalLogRedactor.Pseudonymize(interactionId, OperationalLogIdentifierCategory.Call));
        }
    }

    private static ContactCenterVoiceProviderResult TransferSuccess(string newAgentChannelId, string bridgeId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderCallId = newAgentChannelId,
            ProviderLegId = newAgentChannelId,
            Metadata = new Dictionary<string, string>
            {
                [AsteriskVoiceResultMetadata.TransferNewChannelId] = newAgentChannelId,
                [AsteriskVoiceResultMetadata.TransferBridgeId] = bridgeId,
            },
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> ConferenceAsync(
        ContactCenterVoiceConferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to add a conference participant.");
        }

        // The caller channel identifies the live conversation the participant joins. Only the caller leg is needed to
        // resolve the canonical bridge and its owner; any additional provider call ids in the request are ignored.
        var callerChannelId = request.ProviderCallIds?
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))?
            .Trim();

        if (string.IsNullOrWhiteSpace(callerChannelId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to add a conference participant.");
        }

        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.ConferenceMetadata.AgentUserId, out var targetUserId) ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return Failure("conference_target_missing", "A participant agent is required to add to the conference.");
        }

        // Resolve the canonical conversation bridge and its current owner from a binding owned by THIS tenant's store
        // BEFORE resolving anything about the destination. Failing closed here first enforces CC-1 — an agent can
        // never add a participant to a call this tenant does not own — and guarantees an unowned call always fails
        // with the single unambiguous ownership reason rather than leaking the destination's registration state.
        var currentLeg = await ResolveOwnedAgentLegAsync(callerChannelId);

        if (currentLeg is null)
        {
            return Failure("conference_call_not_owned", "No owned Asterisk conversation bridge was found for the requested conference.");
        }

        var bridgeId = currentLeg.BridgeId;
        var participantChannelId = CreateDeterministicAriId(
            AsteriskAriConstants.ConferenceParticipantChannelPrefix,
            string.Concat(request.InteractionId, "-", targetUserId));

        return await AddAgentToConversationAsync(
            request.InteractionId,
            callerChannelId,
            bridgeId,
            participantChannelId,
            targetUserId,
            "conference",
            ConferenceSuccess,
            cancellationToken);
    }

    /// <summary>
    /// Rings a resolved agent into a live conversation as a non-owning leg on the canonical bridge, shared by the
    /// conference-add and attended-transfer consult flows. The <paramref name="operation"/> word forms the returned
    /// error codes (<c>{operation}_target_offline</c>, <c>{operation}_no_answer</c>, <c>{operation}_failed</c>,
    /// <c>{operation}_outcome_unknown</c>) so each caller keeps its own stable code surface, while the ownership,
    /// idempotency, and compensation invariants are identical because both flows add a Joining leg that never owns
    /// the shared bridge.
    /// </summary>
    /// <param name="interactionId">The interaction whose live conversation the agent joins.</param>
    /// <param name="callerChannelId">The caller channel that anchors the canonical conversation bridge.</param>
    /// <param name="bridgeId">The canonical conversation bridge identifier.</param>
    /// <param name="participantChannelId">The deterministic channel id to originate the agent leg onto.</param>
    /// <param name="targetUserId">The Orchard user id of the agent to add.</param>
    /// <param name="operation">The operation word used to form stable error codes.</param>
    /// <param name="successFactory">Builds the caller-specific success result from the participant channel and bridge.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    private async Task<ContactCenterVoiceProviderResult> AddAgentToConversationAsync(
        string interactionId,
        string callerChannelId,
        string bridgeId,
        string participantChannelId,
        string targetUserId,
        string operation,
        Func<string, string, ContactCenterVoiceProviderResult> successFactory,
        CancellationToken cancellationToken)
    {
        // A retried add for the same participant must be idempotent: if the deterministic participant leg already
        // exists as a live member (a stabilized Participating leg, or the Connected owner when the target agent is
        // already on the call), the add already completed, so re-originating would ring the agent a second time.
        // Confirm the completed add instead.
        var existingMember = await _channelTenantBindingStore.FindByChannelIdAsync(participantChannelId);

        if (existingMember is not null &&
            (existingMember.State == AsteriskChannelBindingState.Participating ||
                existingMember.State == AsteriskChannelBindingState.Connected))
        {
            return successFactory(participantChannelId, bridgeId);
        }

        var destinationEndpoint = await ResolveLiveSoftphoneEndpointAsync(targetUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(destinationEndpoint))
        {
            return Failure($"{operation}_target_offline", "The target agent has no live Asterisk softphone registration to add to the conversation.");
        }

        // Persist the exactly-once ownership claim on the deterministic participant channel id BEFORE originating, as
        // a NON-OWNING Joining leg. The durable Joining binding is BOTH the per-agent add claim — a concurrent
        // duplicate add for the same agent loses this serialized create and must not re-ring — AND the guarantee that
        // ANY death of the participant leg (before, during, or after bridging but before it is stabilized) is a
        // teardown no-op that leaves the live conversation intact, because a Joining leg never owns the shared
        // canonical bridge. The BridgeId is recorded so the leg can be added to the conversation and later promoted,
        // but it is not an ownership claim while the state is Joining.
        var claimed = await _channelTenantBindingStore.CreateAsync(new AsteriskChannelTenantBinding
        {
            ChannelId = participantChannelId,
            ProviderName = TechnicalName,
            InteractionId = interactionId,
            ProviderCallId = callerChannelId,
            BridgeId = bridgeId,
            PeerChannelId = callerChannelId,
            State = AsteriskChannelBindingState.Joining,
            CreatedUtc = _clock.UtcNow,
        });

        if (!claimed)
        {
            // The serialized durable create lost. Distinguish an ACTIVE concurrent duplicate add for the same agent
            // (an in-flight Joining claim or an already-stabilized live member — the outcome is identical, so confirm
            // success without re-ringing) from a STALE claim: a prior attempt's participant leg whose terminal event
            // left a Terminating recovery record for the reconciler to reclaim. Reporting success for a stale claim
            // would falsely confirm a participant that has no live leg, so fail closed (confirmed, retryable) instead
            // and let the caller retry once the stale claim is retired.
            var existingClaim = await _channelTenantBindingStore.FindByChannelIdAsync(participantChannelId);

            if (existingClaim is not null &&
                (existingClaim.State == AsteriskChannelBindingState.Joining ||
                    existingClaim.State == AsteriskChannelBindingState.Participating ||
                    existingClaim.State == AsteriskChannelBindingState.Connected))
            {
                return successFactory(participantChannelId, bridgeId);
            }

            return Failure($"{operation}_failed", "A previous add for this agent is still being reclaimed; retry shortly.");
        }

        var originateAttempted = false;
        var participantBridged = false;

        try
        {
            // Register readiness before originating so the participant leg's StasisStart can never be missed between
            // the originate call returning and the wait beginning. The originate uses our deterministic channel id,
            // so the readiness key matches the id the StasisStart will carry.
            using var readyRegistration = _agentChannelReadySignal.Register(participantChannelId);

            var originateRequest = new AsteriskAriOriginateRequest
            {
                Endpoint = destinationEndpoint,
                CallerId = callerChannelId,
                ChannelId = participantChannelId,
                AppArgs = [AsteriskConstants.OriginationMarkerVariableName, interactionId, "agent"],
                Variables = new Dictionary<string, string>
                {
                    [AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue,
                    [AsteriskConstants.InteractionChannelVariableName] = interactionId,
                },
            };

            // Mark the originate as attempted BEFORE issuing it: an ambiguous transport failure (no ARI response) may
            // still have created the participant channel under our deterministic id, so the compensation path must be
            // able to best-effort release it even though the originate never returned success.
            originateAttempted = true;

            await _ariClient.OriginateAsync(originateRequest, cancellationToken);

            // The participant can only be bridged once it has entered Stasis (that is, once the participant agent
            // answers), so wait for its owned-origination StasisStart bounded by the answer timeout. The existing
            // parties keep talking on the canonical bridge until the participant answers, so an add to an agent who
            // never answers leaves the ORIGINAL conversation fully intact.
            var participantReady = await readyRegistration.WaitAsync(
                TimeSpan.FromSeconds(AsteriskAriConstants.AgentAnswerTimeoutSeconds),
                cancellationToken);

            if (!participantReady)
            {
                // A cancelled wait surfaces as a not-ready result, so distinguish genuine no-answer from cancellation
                // and let cancellation propagate to the unknown-outcome path instead of reporting a confirmed timeout.
                cancellationToken.ThrowIfCancellationRequested();

                // The participant leg was originated and is ringing (it simply never answered), so its channel is
                // live. Best-effort hang it up; if that hangup is not confirmed, keep a durable recovery record so
                // the reconciler reclaims the dangling channel instead of leaking it.
                var noAnswerHungUp = await TryHangupAsync(participantChannelId, CancellationToken.None);
                await RetireProvisionalTransferClaimAsync(participantChannelId, noAnswerHungUp);

                return Failure($"{operation}_no_answer", "The target agent did not answer before the add timed out.");
            }

            // Commit point: add the answered participant leg to the SAME canonical conversation bridge the existing
            // parties are already on, so recording stays on one continuous bridge and every party hears the new
            // participant. The participant binding is still Joining (non-owning), so a participant death anywhere in
            // this window is a teardown no-op that leaves the existing conversation intact. The add becomes a durable
            // member only when FinalizeConferenceParticipantAsync promotes it to the stable Participating phase.
            await _ariClient.AddChannelToBridgeAsync(bridgeId, participantChannelId, cancellationToken);
            participantBridged = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to add an agent to interaction {InteractionId}; compensating the participant leg.",
                OperationalLogRedactor.Pseudonymize(interactionId, OperationalLogIdentifierCategory.Call));

            // Pre-commit compensation. Because the serialized durable Joining create above gave THIS add exclusive
            // ownership of the deterministic participant id (a concurrent duplicate lost the create and never
            // originated), releasing our own participant leg can never touch another add's live call — no existence
            // probe or winner check is needed. A Joining leg never owns the shared bridge, so compensating in any
            // order can never drop the existing conversation.
            if (!participantBridged)
            {
                var channelDefinitelyRejected = ex is AsteriskAriException rejected && rejected.StatusCode is not null;
                var channelMayExist = originateAttempted && !channelDefinitelyRejected;
                var channelConfirmedGone = true;

                if (channelMayExist)
                {
                    channelConfirmedGone = await TryHangupAsync(participantChannelId, CancellationToken.None);
                }

                await RetireProvisionalTransferClaimAsync(participantChannelId, channelConfirmedGone);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            var outcomeUnknown = ex is AsteriskAriException ariException && IsAmbiguousAriOutcome(ariException);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = outcomeUnknown,
                ProviderName = TechnicalName,
                ProviderCallId = callerChannelId,
                ErrorCode = outcomeUnknown ? $"{operation}_outcome_unknown" : $"{operation}_failed",
                ErrorMessage = "The Asterisk add of an agent to the conversation could not be completed.",
            };
        }

        // Post-commit stabilization: the participant leg is bridged with the existing parties, so the add has
        // physically succeeded and is reported as success. Promote it from the provisioning Joining phase to the
        // stable NON-OWNING Participating phase so the reconciler treats the live member as healthy rather than an
        // aged, never-committed join to reclaim — never rolling back the bridged leg — leaving any residue to the
        // terminal-event/reconciler path, exactly as the transfer flow tolerates post-commit ARI hiccups.
        await FinalizeConferenceParticipantAsync(participantChannelId, interactionId);

        return successFactory(participantChannelId, bridgeId);
    }

    private async Task FinalizeConferenceParticipantAsync(string participantChannelId, string interactionId)
    {
        try
        {
            // Promote the bridged participant leg from Joining to the stable non-owning Participating phase. Until
            // this commits the leg is Joining and the reconciler would age-reclaim a still-alive participant as a
            // never-committed join; promoting it makes the reconciler treat the live member as healthy.
            var promoted = await _channelTenantBindingStore.TryPromoteJoiningToParticipatingAsync(participantChannelId);

            if (!promoted)
            {
                // A terminal event claimed the participant leg for teardown between the bridge commit and this
                // promotion. The dangling participant residue is reclaimed by the reconciler; the existing parties
                // are unaffected because a Joining/Participating leg never owns the shared bridge, so the physically
                // committed add is still a success.
                _logger.LogWarning(
                    "Asterisk conference add of interaction {InteractionId} bridged the participant but could not stabilize it because its leg was already claimed for teardown; leaving the residue for the reconciler.",
                    OperationalLogRedactor.Pseudonymize(interactionId, OperationalLogIdentifierCategory.Call));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk conference add of interaction {InteractionId} bridged the participant but did not stabilize its leg cleanly; leaving residue for the reconciler.",
                OperationalLogRedactor.Pseudonymize(interactionId, OperationalLogIdentifierCategory.Call));
        }
    }

    private static ContactCenterVoiceProviderResult ConferenceSuccess(string participantChannelId, string bridgeId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            Metadata = new Dictionary<string, string>
            {
                [AsteriskVoiceResultMetadata.ConferenceParticipantChannelId] = participantChannelId,
                [AsteriskVoiceResultMetadata.ConferenceBridgeId] = bridgeId,
            },
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> BeginConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var (failure, context) = await ResolveConsultContextAsync(request, "consult");

        if (failure is not null)
        {
            return failure;
        }

        // Hold the customer BEFORE ringing the destination agent so the private consult can never be heard by the
        // customer. Holding fails closed: if the hold does not commit, the destination is never rung, leaving the
        // original conversation fully intact rather than opening a three-way call the customer can overhear.
        var held = await TryHoldAsync(context.CallerChannelId, cancellationToken);

        if (!held)
        {
            return Failure("consult_hold_failed", "The customer could not be placed on hold to begin the private consult.");
        }

        ContactCenterVoiceProviderResult addResult;

        try
        {
            // Ring the destination agent into the SAME canonical bridge as a non-owning participant, reusing the
            // conference-add core. The customer is held, so the destination agent and the initiating agent hold a
            // private conversation; if the destination never answers the add leaves the original call intact.
            addResult = await AddAgentToConversationAsync(
                request.InteractionId,
                context.CallerChannelId,
                context.BridgeId,
                context.ConsultChannelId,
                context.TargetUserId,
                "consult",
                ConsultSuccess,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The add compensated its own destination leg before rethrowing, but the customer is still held. Resume
            // the customer so a cancelled begin-consult never strands them on hold, then propagate the cancellation.
            await TryUnholdAsync(context.CallerChannelId, CancellationToken.None);

            throw;
        }

        if (!addResult.Succeeded)
        {
            // The destination agent did not join (offline, no answer, or a failed originate). The add already
            // compensated its own leg, so resume the customer with the initiating agent and surface the failure.
            await TryUnholdAsync(context.CallerChannelId, CancellationToken.None);

            return addResult;
        }

        return addResult;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> CompleteConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var (failure, context) = await ResolveConsultContextAsync(request, "consult_complete");

        if (failure is not null)
        {
            return failure;
        }

        // Resume the customer BEFORE the ownership swap: if the atomic hand-off cannot commit (the destination leg is
        // gone), the customer is already back in a live conversation with the initiating agent who still owns the
        // bridge, rather than being stranded on hold. Unhold is best-effort; a failed unhold is a recoverable degraded
        // state (the customer stays on hold with a live owner) that never drops the call.
        await TryUnholdAsync(context.CallerChannelId, cancellationToken);

        // Atomically promote the stabilized destination participant to the Connected owner AND retire the initiating
        // agent leg in one transaction, so the canonical bridge is owned by exactly one Connected binding at every
        // instant — the initiating agent before the swap, the destination agent after it.
        var swapped = await _channelTenantBindingStore.PromoteParticipantToConnectedOwnerAsync(
            context.ConsultChannelId,
            context.AgentChannelId);

        if (!swapped)
        {
            // The destination leg was no longer a live participant (the consult was never begun, or the destination
            // hung up), so ownership was not handed off. The customer safely keeps the initiating agent. This is a
            // CONFIRMED failure — the call is intact, just not transferred.
            return Failure("consult_complete_no_target", "No live consult leg was found to complete the attended transfer; the customer remains with the initiating agent.");
        }

        // The destination leg now solely owns the canonical bridge and the initiating leg was atomically retired to a
        // non-owning recovery record, so hanging up the initiating leg cannot tear down the conversation. Best-effort
        // hang it up and reconcile its record, exactly as the blind-transfer handoff does.
        var previousHungUp = await TryHangupAsync(context.AgentChannelId, CancellationToken.None);
        await RetireProvisionalTransferClaimAsync(context.AgentChannelId, previousHungUp);

        return ConsultSuccess(context.ConsultChannelId, context.BridgeId);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> CancelConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        var (failure, context) = await ResolveConsultContextAsync(request, "consult_cancel");

        if (failure is not null)
        {
            return failure;
        }

        // Drop the destination consult leg first, but atomically and ONLY while it is a non-owning member leg. The
        // consult channel id is deterministic and is the SAME id CompleteConsultAsync promotes IN PLACE from
        // Participating to the sole Connected owner of the canonical bridge, so a bare read-then-hangup could hang up
        // that promoted owner and drop the live customer. TryClaimProvisionalLegForTeardownAsync is the linearization
        // point: it version-checks and transitions the leg to Terminating ONLY when it is still Joining/Participating,
        // so a completion that already (or concurrently) promoted it to Connected makes this claim fail and the cancel
        // never hangs up the leg. When the claim succeeds we own the teardown of this dangling channel.
        var claimedConsultLeg = await _channelTenantBindingStore.TryClaimProvisionalLegForTeardownAsync(context.ConsultChannelId);

        if (claimedConsultLeg)
        {
            var consultHungUp = await TryHangupAsync(context.ConsultChannelId, CancellationToken.None);

            if (consultHungUp)
            {
                // The channel is confirmed gone, so retire the durable teardown record. Otherwise leave the
                // Terminating record (with its non-owning pre-teardown disposition) for the reconciler to reclaim.
                await _channelTenantBindingStore.RemoveByChannelIdAsync(context.ConsultChannelId);
            }
        }

        // Resume the customer with the initiating agent, who remained the Connected owner throughout the consult, so
        // ownership is unchanged and the original conversation continues.
        await TryUnholdAsync(context.CallerChannelId, cancellationToken);

        return ConsultSuccess(context.ConsultChannelId, context.BridgeId);
    }

    private async Task<(ContactCenterVoiceProviderResult Failure, ResolvedConsultContext Context)> ResolveConsultContextAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        string operation)
    {
        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return (Failure("interaction_missing", "An interaction id is required for the attended transfer."), null);
        }

        var callerChannelId = request.ProviderCallId?.Trim();

        if (string.IsNullOrWhiteSpace(callerChannelId))
        {
            return (Failure("caller_channel_missing", "An Asterisk caller channel id is required for the attended transfer."), null);
        }

        if (request.Metadata is null ||
            !request.Metadata.TryGetValue(ContactCenterConstants.AttendedTransferMetadata.AgentUserId, out var targetUserId) ||
            string.IsNullOrWhiteSpace(targetUserId))
        {
            return (Failure($"{operation}_target_missing", "A destination agent is required for the attended transfer."), null);
        }

        // Resolve the canonical conversation bridge and its current Connected owner from a binding owned by THIS
        // tenant's store. Failing closed here enforces CC-1 — an agent can never consult on a call this tenant does
        // not own — and guarantees an unowned call fails with the single unambiguous ownership reason.
        var currentLeg = await ResolveOwnedAgentLegAsync(callerChannelId);

        if (currentLeg is null)
        {
            return (Failure($"{operation}_call_not_owned", "No owned Asterisk conversation bridge was found for the requested attended transfer."), null);
        }

        var consultChannelId = CreateDeterministicAriId(
            AsteriskAriConstants.AttendedConsultChannelPrefix,
            string.Concat(request.InteractionId, "-", targetUserId));

        var context = new ResolvedConsultContext
        {
            CallerChannelId = callerChannelId,
            BridgeId = currentLeg.BridgeId,
            AgentChannelId = currentLeg.ChannelId,
            TargetUserId = targetUserId,
            ConsultChannelId = consultChannelId,
        };

        return (null, context);
    }

    private static ContactCenterVoiceProviderResult ConsultSuccess(string consultChannelId, string bridgeId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            Metadata = new Dictionary<string, string>
            {
                [AsteriskVoiceResultMetadata.AttendedTransferConsultChannelId] = consultChannelId,
                [AsteriskVoiceResultMetadata.AttendedTransferBridgeId] = bridgeId,
            },
        };
    }

    private async Task<bool> TryHoldAsync(string channelId, CancellationToken cancellationToken)
    {
        try
        {
            await _ariClient.HoldChannelAsync(channelId, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk could not place the customer channel on hold for an attended transfer.");

            return false;
        }
    }

    private async Task<bool> TryUnholdAsync(string channelId, CancellationToken cancellationToken)
    {
        try
        {
            await _ariClient.UnholdChannelAsync(channelId, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk could not resume the customer channel from hold for an attended transfer.");

            return false;
        }
    }

    private sealed class ResolvedConsultContext
    {
        public string CallerChannelId { get; init; }

        public string BridgeId { get; init; }

        public string AgentChannelId { get; init; }

        public string TargetUserId { get; init; }

        public string ConsultChannelId { get; init; }
    }


    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> EngageAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to start a supervisor engagement.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to derive the supervisor engagement identity.");
        }

        if (string.IsNullOrWhiteSpace(request.SupervisorId))
        {
            return Failure("supervisor_missing", "A supervisor id is required to start a supervisor engagement.");
        }

        var stableKey = CreateSupervisorEngagementKey(request.InteractionId, request.SupervisorId);

        // Serialize every start and stop that shares this engagement identity within the node. The supervisor
        // resource ids are deterministic from the engagement key, so two concurrent starts would otherwise both
        // register a readiness waiter for the same channel id — the second registration forces the first waiter to
        // false and makes the first attempt compensate the shared, still-live engagement. Combined with the
        // idempotency guard below, the second serialized start observes the existing supervisor channel and returns
        // success instead of racing readiness.
        using var engagementLock = await AsteriskSupervisorEngagementLock.AcquireAsync(stableKey, cancellationToken);

        // Resolve the tenant-owned conversation topology from a binding in THIS tenant's store. The agent-leg binding
        // carries both the mixing (conversation) bridge and the agent channel to snoop. Failing closed when no owning
        // binding exists enforces CC-1: a supervisor can never engage a call this tenant does not own.
        var agentLeg = await ResolveOwnedAgentLegAsync(request.ProviderCallId);

        if (agentLeg is null)
        {
            return Failure("monitor_call_not_owned", "No owned Asterisk conversation was found for the requested supervisor engagement.");
        }

        var supervisorBridgeId = CreateDeterministicAriId(AsteriskAriConstants.SupervisorBridgePrefix, stableKey);
        var supervisorChannelId = CreateDeterministicAriId(AsteriskAriConstants.SupervisorChannelPrefix, stableKey);
        var snoopChannelId = CreateDeterministicAriId(AsteriskAriConstants.SupervisorSnoopPrefix, stableKey);

        // Non-mutating idempotency probe, deliberately OUTSIDE any compensation scope. The probe creates nothing, so
        // a transient failure of it must classify an outcome per CC-2 and never tear down a possibly-live engagement
        // that a prior start established.
        bool supervisorLegAlreadyPresent;

        try
        {
            supervisorLegAlreadyPresent = await _ariClient.ChannelExistsAsync(supervisorChannelId, cancellationToken);
        }
        catch (AsteriskAriException ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk could not probe the existing {MonitorMode} supervisor leg for interaction {InteractionId}; leaving any established engagement untouched.",
                request.Mode,
                OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

            return MonitoringFailure(ex, request);
        }

        if (supervisorLegAlreadyPresent)
        {
            // A prior start already originated this deterministic supervisor leg. Channel existence alone does not
            // prove the snoop and bridge topology is complete, so re-assert the idempotent topology instead of
            // trusting the leg's presence: an existing bridge or snoop is returned as-is and an already-member
            // channel add is a no-op, so this completes a partially-established engagement and confirms a
            // fully-established one. Because this invocation originates no new leg, a transient failure here
            // classifies an outcome WITHOUT compensating, so a live engagement is never torn down.
            try
            {
                await ReassertSupervisorTopologyAsync(
                    request.Mode,
                    agentLeg.ChannelId,
                    agentLeg.BridgeId,
                    supervisorBridgeId,
                    supervisorChannelId,
                    snoopChannelId,
                    cancellationToken);

                return request.Mode == MonitorMode.Barge
                    ? MonitoringSuccess(request.Mode, supervisorChannelId, snoopChannelId: null, agentLeg.BridgeId)
                    : MonitoringSuccess(request.Mode, supervisorChannelId, snoopChannelId, supervisorBridgeId);
            }
            catch (AsteriskAriException ex)
            {
                _logger.LogError(
                    OperationalLogRedactor.RedactException(ex),
                    "Asterisk could not re-assert the {MonitorMode} supervisor topology for interaction {InteractionId}; leaving the existing engagement untouched.",
                    request.Mode,
                    OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

                return MonitoringFailure(ex, request);
            }
        }

        // The supervisor has no audio without a real leg, so resolve their live browser softphone endpoint the same
        // way the connect flow resolves an agent's. When the supervisor is not registered, fail closed rather than
        // create a snoop and bridge that no one can hear.
        var supervisorEndpoint = await ResolveSupervisorEndpointAsync(request.SupervisorId, cancellationToken);

        if (string.IsNullOrWhiteSpace(supervisorEndpoint))
        {
            return Failure("supervisor_endpoint_missing", "The supervisor has no live Asterisk softphone registration to engage the call with.");
        }

        try
        {
            return request.Mode switch
            {
                MonitorMode.Monitor => await EngageListeningAsync(
                    request,
                    agentLeg.ChannelId,
                    supervisorEndpoint,
                    supervisorBridgeId,
                    supervisorChannelId,
                    snoopChannelId,
                    AsteriskAriConstants.SnoopWhisperNone,
                    cancellationToken),
                MonitorMode.Whisper => await EngageListeningAsync(
                    request,
                    agentLeg.ChannelId,
                    supervisorEndpoint,
                    supervisorBridgeId,
                    supervisorChannelId,
                    snoopChannelId,
                    AsteriskAriConstants.SnoopWhisperOut,
                    cancellationToken),
                MonitorMode.Barge => await EngageBargeAsync(
                    request,
                    agentLeg.BridgeId,
                    supervisorEndpoint,
                    supervisorChannelId,
                    cancellationToken),
                _ => Failure("monitor_mode_unsupported", "The requested supervisor engagement mode is not supported."),
            };
        }
        catch (AsteriskAriException ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to start the {MonitorMode} supervisor engagement for interaction {InteractionId}; compensating supervisor-only side effects.",
                request.Mode,
                OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

            // Compensation is scoped to this engagement's own deterministic supervisor resources; it never touches
            // the customer/agent call. It is safe here because the supervisor leg did not exist when this invocation
            // started (verified by the probe above under the engagement lock), so every resource torn down was
            // created by this invocation. It is best-effort because the returned outcome is classified from the
            // original failure, not from the cleanup.
            await CompensateSupervisorEngagementAsync(
                request.Mode,
                agentLeg.BridgeId,
                supervisorBridgeId,
                supervisorChannelId,
                snoopChannelId);

            return MonitoringFailure(ex, request);
        }
        catch (Exception ex)
        {
            // Cancellation or any unexpected failure thrown AFTER a supervisor bridge, snoop, or channel was created
            // must not leak those legs. Compensate with a non-cancellable token (the ambient token may already be
            // cancelled), then rethrow so cancellation semantics are preserved — the service layer maps a cancelled
            // engagement to an unknown outcome rather than a false success.
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "The {MonitorMode} supervisor engagement for interaction {InteractionId} failed unexpectedly; compensating supervisor-only side effects.",
                request.Mode,
                OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

            await CompensateSupervisorEngagementAsync(
                request.Mode,
                agentLeg.BridgeId,
                supervisorBridgeId,
                supervisorChannelId,
                snoopChannelId);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> StopAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to derive the supervisor engagement identity.");
        }

        if (string.IsNullOrWhiteSpace(request.SupervisorId))
        {
            return Failure("supervisor_missing", "A supervisor id is required to stop a supervisor engagement.");
        }

        // Recompute the same deterministic ids the engagement created so the stop addresses exactly this
        // supervisor's legs without any extra stored state.
        var stableKey = CreateSupervisorEngagementKey(request.InteractionId, request.SupervisorId);
        var supervisorBridgeId = CreateDeterministicAriId(AsteriskAriConstants.SupervisorBridgePrefix, stableKey);
        var supervisorChannelId = CreateDeterministicAriId(AsteriskAriConstants.SupervisorChannelPrefix, stableKey);
        var snoopChannelId = CreateDeterministicAriId(AsteriskAriConstants.SupervisorSnoopPrefix, stableKey);

        // Serialize the stop against any concurrent start or stop for the same engagement identity so a stop can
        // never interleave with an in-flight start's bridge/originate/snoop sequence for the same deterministic ids.
        using var engagementLock = await AsteriskSupervisorEngagementLock.AcquireAsync(stableKey, cancellationToken);

        // A barge leg lives in the main conversation bridge, so it must be detached from that bridge before it is
        // hung up. Resolve the owning bridge from the tenant binding; when the call is no longer owned there is
        // nothing to detach from and the supervisor-leg hangup alone is sufficient.
        string mixingBridgeId = null;

        if (request.Mode == MonitorMode.Barge && !string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            var agentLeg = await ResolveOwnedAgentLegAsync(request.ProviderCallId);
            mixingBridgeId = agentLeg?.BridgeId;
        }

        try
        {
            await TeardownSupervisorEngagementAsync(
                request.Mode,
                mixingBridgeId,
                supervisorBridgeId,
                supervisorChannelId,
                snoopChannelId,
                cancellationToken);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId,
                Metadata = new Dictionary<string, string>
                {
                    [AsteriskVoiceResultMetadata.MonitoringMode] = request.Mode.ToString(),
                },
            };
        }
        catch (AsteriskAriException ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk failed to stop the {MonitorMode} supervisor engagement for interaction {InteractionId}.",
                request.Mode,
                OperationalLogRedactor.Pseudonymize(request.InteractionId, OperationalLogIdentifierCategory.Call));

            var outcomeUnknown = IsAmbiguousAriOutcome(ex);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = outcomeUnknown,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId,
                ErrorCode = outcomeUnknown ? "monitor_stop_outcome_unknown" : "monitor_stop_failed",
                ErrorMessage = "The Asterisk supervisor engagement stop could not be confirmed.",
            };
        }
    }

    private async Task<ContactCenterVoiceProviderResult> EngageListeningAsync(
        ContactCenterVoiceMonitoringRequest request,
        string agentChannelId,
        string supervisorEndpoint,
        string supervisorBridgeId,
        string supervisorChannelId,
        string snoopChannelId,
        string whisperDirection,
        CancellationToken cancellationToken)
    {
        // A snoop channel alone gives the supervisor no audio path, so a dedicated supervisor mixing bridge joins the
        // snoop (which carries the conversation audio) to an originated supervisor endpoint. The bridge is created
        // first so the supervisor leg has somewhere to be placed the instant it answers.
        await _ariClient.CreateBridgeAsync(supervisorBridgeId, AsteriskAriConstants.MixingBridgeType, cancellationToken);

        var ready = await OriginateSupervisorLegAsync(request.InteractionId, supervisorEndpoint, supervisorChannelId, cancellationToken);

        if (!ready)
        {
            await CompensateSupervisorEngagementAsync(
                request.Mode,
                mixingBridgeId: null,
                supervisorBridgeId,
                supervisorChannelId,
                snoopChannelId);

            return Failure("supervisor_no_answer", "The supervisor did not answer before the engagement timed out.");
        }

        // Snoop the AGENT channel with spy=both so the supervisor hears both parties. whisper=none keeps the
        // supervisor silent (Monitor); whisper=out injects the supervisor audio outward into the agent channel only
        // (Whisper), so the customer never hears the supervisor.
        await _ariClient.SnoopChannelAsync(
            agentChannelId,
            AsteriskAriConstants.SnoopSpyBoth,
            whisperDirection,
            snoopChannelId,
            cancellationToken);

        await _ariClient.AddChannelToBridgeAsync(supervisorBridgeId, snoopChannelId, cancellationToken);
        await _ariClient.AddChannelToBridgeAsync(supervisorBridgeId, supervisorChannelId, cancellationToken);

        return MonitoringSuccess(request.Mode, supervisorChannelId, snoopChannelId, supervisorBridgeId);
    }

    private async Task<ContactCenterVoiceProviderResult> EngageBargeAsync(
        ContactCenterVoiceMonitoringRequest request,
        string mixingBridgeId,
        string supervisorEndpoint,
        string supervisorChannelId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mixingBridgeId))
        {
            return Failure("monitor_call_not_owned", "No owned Asterisk conversation bridge was found for the requested barge engagement.");
        }

        var ready = await OriginateSupervisorLegAsync(request.InteractionId, supervisorEndpoint, supervisorChannelId, cancellationToken);

        if (!ready)
        {
            await CompensateSupervisorEngagementAsync(
                MonitorMode.Barge,
                mixingBridgeId,
                supervisorBridgeId: null,
                supervisorChannelId,
                snoopChannelId: null);

            return Failure("supervisor_no_answer", "The supervisor did not answer before the engagement timed out.");
        }

        // Barge adds the supervisor directly into the main conversation bridge, so all parties hear the supervisor.
        await _ariClient.AddChannelToBridgeAsync(mixingBridgeId, supervisorChannelId, cancellationToken);

        return MonitoringSuccess(MonitorMode.Barge, supervisorChannelId, snoopChannelId: null, mixingBridgeId);
    }

    private async Task ReassertSupervisorTopologyAsync(
        MonitorMode mode,
        string agentChannelId,
        string mixingBridgeId,
        string supervisorBridgeId,
        string supervisorChannelId,
        string snoopChannelId,
        CancellationToken cancellationToken)
    {
        // Re-assert the audio topology for an engagement whose supervisor leg already exists. Every operation is
        // idempotent, so this completes a partially-established engagement and confirms a fully-established one
        // without ever originating a new leg or registering a readiness waiter.
        if (mode == MonitorMode.Barge)
        {
            // The barge leg belongs in the main conversation bridge; re-adding an already-member channel is a no-op,
            // so this simply confirms the supervisor leg is attached to the shared audio path.
            await _ariClient.AddChannelToBridgeAsync(mixingBridgeId, supervisorChannelId, cancellationToken);

            return;
        }

        var whisperDirection = mode == MonitorMode.Whisper
            ? AsteriskAriConstants.SnoopWhisperOut
            : AsteriskAriConstants.SnoopWhisperNone;

        // Monitor and Whisper own the dedicated supervisor bridge and snoop. An existing bridge or snoop is returned
        // as-is (409 read-back) and an already-member channel add is a no-op, so re-asserting joins any missing piece
        // of the topology while leaving a complete one undisturbed.
        await _ariClient.CreateBridgeAsync(supervisorBridgeId, AsteriskAriConstants.MixingBridgeType, cancellationToken);

        await _ariClient.SnoopChannelAsync(
            agentChannelId,
            AsteriskAriConstants.SnoopSpyBoth,
            whisperDirection,
            snoopChannelId,
            cancellationToken);

        await _ariClient.AddChannelToBridgeAsync(supervisorBridgeId, snoopChannelId, cancellationToken);
        await _ariClient.AddChannelToBridgeAsync(supervisorBridgeId, supervisorChannelId, cancellationToken);
    }

    private ContactCenterVoiceProviderResult MonitoringFailure(
        AsteriskAriException exception,
        ContactCenterVoiceMonitoringRequest request)
    {
        // A genuinely ambiguous transport outcome is reported as unknown so the service layer never treats an
        // unconfirmed supervisor engagement as either a clean success or a clean failure (CC-2); a definite failure
        // (a received error status or a local pre-flight rejection) is reported as a plain failure.
        var outcomeUnknown = IsAmbiguousAriOutcome(exception);

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            OutcomeUnknown = outcomeUnknown,
            ProviderName = TechnicalName,
            ProviderCallId = request.ProviderCallId,
            ErrorCode = outcomeUnknown ? "monitor_outcome_unknown" : "monitor_failed",
            ErrorMessage = "The Asterisk supervisor engagement could not be confirmed.",
        };
    }

    private async Task<bool> OriginateSupervisorLegAsync(
        string interactionId,
        string supervisorEndpoint,
        string supervisorChannelId,
        CancellationToken cancellationToken)
    {
        // Register readiness before originating so the supervisor leg's StasisStart can never be missed between the
        // originate call returning and the wait beginning. The originate uses our deterministic channel id, so the
        // readiness key matches the id the StasisStart will carry.
        using var readyRegistration = _agentChannelReadySignal.Register(supervisorChannelId);

        await _ariClient.OriginateAsync(new AsteriskAriOriginateRequest
        {
            Endpoint = supervisorEndpoint,
            ChannelId = supervisorChannelId,
            AppArgs = [AsteriskConstants.OriginationMarkerVariableName, interactionId ?? string.Empty, "supervisor"],
            Variables = new Dictionary<string, string>
            {
                [AsteriskConstants.OriginationMarkerVariableName] = AsteriskAriConstants.OriginationMarkerValue,
                [AsteriskConstants.InteractionChannelVariableName] = interactionId ?? string.Empty,
            },
        }, cancellationToken);

        // An ARI originate returns as soon as the channel is dialing, not when the supervisor answers. The channel
        // can only be bridged once it has entered the Stasis application, so wait for its owned-origination
        // StasisStart (bounded by the answer timeout) before bridging.
        return await readyRegistration.WaitAsync(
            TimeSpan.FromSeconds(AsteriskAriConstants.SupervisorAnswerTimeoutSeconds),
            cancellationToken);
    }

    private async Task CompensateSupervisorEngagementAsync(
        MonitorMode mode,
        string mixingBridgeId,
        string supervisorBridgeId,
        string supervisorChannelId,
        string snoopChannelId)
    {
        // Best-effort compensation for a failed engagement: swallow cleanup errors because the caller derives its
        // outcome from the original failure, and never touch the customer/agent call.
        try
        {
            await TeardownSupervisorEngagementAsync(
                mode,
                mixingBridgeId,
                supervisorBridgeId,
                supervisorChannelId,
                snoopChannelId,
                CancellationToken.None);
        }
        catch (AsteriskAriException ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk supervisor engagement compensation did not complete cleanly.");
        }
    }

    private async Task TeardownSupervisorEngagementAsync(
        MonitorMode mode,
        string mixingBridgeId,
        string supervisorBridgeId,
        string supervisorChannelId,
        string snoopChannelId,
        CancellationToken cancellationToken)
    {
        // Every ARI operation here is idempotent (the client treats an already-gone channel or bridge as success), so
        // a stop of an engagement whose legs are already gone — including a double-stop — succeeds. A genuine
        // transport failure still throws so the caller can classify the outcome per CC-2.
        if (mode == MonitorMode.Barge)
        {
            // A barge leg lives inside the main conversation bridge, which this engagement does NOT own, so only
            // detach the supervisor leg from it and hang the supervisor leg up. The customer/agent call is untouched.
            if (!string.IsNullOrWhiteSpace(mixingBridgeId) && !string.IsNullOrWhiteSpace(supervisorChannelId))
            {
                await _ariClient.RemoveChannelFromBridgeAsync(mixingBridgeId, supervisorChannelId, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(supervisorChannelId))
            {
                await _ariClient.HangupAsync(supervisorChannelId, cancellationToken);
            }

            return;
        }

        // Monitor and Whisper own the dedicated supervisor bridge and the snoop leg, so tear all three down.
        if (!string.IsNullOrWhiteSpace(supervisorChannelId))
        {
            await _ariClient.HangupAsync(supervisorChannelId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(snoopChannelId))
        {
            await _ariClient.HangupAsync(snoopChannelId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(supervisorBridgeId))
        {
            await _ariClient.DestroyBridgeAsync(supervisorBridgeId, cancellationToken);
        }
    }

    private static ContactCenterVoiceProviderResult MonitoringSuccess(
        MonitorMode mode,
        string supervisorChannelId,
        string snoopChannelId,
        string bridgeId)
    {
        var metadata = new Dictionary<string, string>
        {
            [AsteriskVoiceResultMetadata.SupervisorChannelId] = supervisorChannelId,
            [AsteriskVoiceResultMetadata.SupervisorBridgeId] = bridgeId,
            [AsteriskVoiceResultMetadata.MonitoringMode] = mode.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(snoopChannelId))
        {
            metadata[AsteriskVoiceResultMetadata.SnoopChannelId] = snoopChannelId;
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            ProviderLegId = supervisorChannelId,
            Metadata = metadata,
        };
    }

    private async Task<AsteriskChannelTenantBinding> ResolveOwnedAgentLegAsync(string providerCallId)
    {
        // The caller-to-agent connect writes the mixing bridge id and the agent channel onto the agent-leg binding
        // whose PeerChannelId is the caller channel, so an engagement request that carries the caller channel id
        // resolves both the conversation bridge and the agent channel to snoop through that binding. Only bindings
        // persisted in this tenant's store are considered, so ownership is structural.
        var peerBindings = await _channelTenantBindingStore.FindAllByPeerChannelIdAsync(providerCallId);

        // Prefer the Connected owner over an in-flight Joining destination leg: during a transfer both a Connected
        // (previous) agent leg and a Joining (destination) leg reference the same caller, and the live conversation
        // agent — the one to resolve for a concurrent transfer's current leg or a monitoring snoop — is the Connected
        // one until the handoff atomically swaps ownership.
        return peerBindings
            .Where(binding =>
                !string.IsNullOrWhiteSpace(binding.BridgeId) &&
                !string.IsNullOrWhiteSpace(binding.ChannelId))
            .OrderBy(binding => binding.State == AsteriskChannelBindingState.Connected ? 0 : 1)
            .FirstOrDefault();
    }

    private Task<string> ResolveSupervisorEndpointAsync(string supervisorId, CancellationToken cancellationToken)
    {
        // A supervisor's reachable endpoint is their browser softphone, resolved from the tenant-scoped lease store
        // exactly like an agent's; when none exists the supervisor is not registered and the engagement fails closed.
        return ResolveLiveSoftphoneEndpointAsync(supervisorId, cancellationToken);
    }

    private async Task<string> ResolveLiveSoftphoneEndpointAsync(string userId, CancellationToken cancellationToken)
    {
        // The user's reachable endpoint is their browser softphone, provisioned per registration as a random PJSIP
        // authorization user rather than a stable extension, so it must be resolved from the tenant-scoped lease
        // store. The newest live lease represents the user's current registration; when none exists the user is not
        // registered and the caller fails closed.
        var liveLeases = await _pjsipCredentialLeaseStore.ListLiveByUserAsync(
            userId.Trim(),
            _clock.UtcNow,
            cancellationToken);

        var activeLease = liveLeases
            .OrderByDescending(lease => lease.IssuedUtc)
            .FirstOrDefault();

        if (activeLease is null || string.IsNullOrWhiteSpace(activeLease.AuthorizationUser))
        {
            return null;
        }

        return string.Concat(
            AsteriskAriConstants.PjsipEndpointTechnology,
            "/",
            activeLease.AuthorizationUser.Trim());
    }

    private static string CreateSupervisorEngagementKey(string interactionId, string supervisorId)
    {
        // Derive the engagement key from the interaction and supervisor identity so every deterministic supervisor
        // resource id is stable and unique to this one supervisor's engagement on this one call: retries are
        // idempotent (same ids), a later stop addresses the same legs, and two supervisors on the same call never
        // collide.
        return string.Concat(interactionId, "-", supervisorId);
    }

    private static bool IsAmbiguousAriOutcome(AsteriskAriException exception)
    {
        // Per CC-2, a supervisor engagement outcome is unknown only when no ARI response was ever observed because
        // the request timed out or Asterisk was unreachable in transit; the client surfaces that as a null-status
        // exception that wraps the underlying transport failure. A received HTTP error response (4xx or 5xx) is a
        // confirmed failure, and a null-status exception with no inner transport failure is a definite local
        // pre-flight rejection (the provider is unconfigured or the tenant does not own the ARI application) that
        // never reached Asterisk, so neither of those is ambiguous.
        return exception.StatusCode is null && exception.InnerException is not null;
    }

    private async Task CompensateAsync(
        string agentChannelId,
        string bridgeId,
        string callerChannelIdToReturn,
        bool provisioningOutcomeAmbiguous,
        CancellationToken cancellationToken)
    {
        // Claim the pre-created Pending binding FIRST so the claim flips it to Terminating with a Pending
        // disposition before the agent hangup below: any terminal event that hangup produces is then read by the
        // teardown pipeline as an already-claimed, Pending-disposition leg (which never hangs up the caller) rather
        // than as a connected leg. Winning the claim also makes THIS flow the owner of the durable record's
        // retirement. Losing it means a terminal-event teardown already owns the Terminating record — or a racing
        // caller teardown already removed it — so this flow must NOT retire the record. It MUST still release the
        // ARI resources it created, because their ids are deterministic and unique to this one attempt (the
        // command-id fence), so destroying them can never touch another attempt and is idempotent with any owner
        // that also cleans them. That unconditional self-cleanup closes the leak where a caller teardown retired the
        // Pending binding by 404-ing on resources this flow had not created yet, and this flow then created them
        // with no durable record left to drive their cleanup.
        var ownsBinding = false;

        if (!string.IsNullOrWhiteSpace(agentChannelId))
        {
            try
            {
                var claim = await _channelTenantBindingStore.TryBeginTeardownAsync(agentChannelId);
                ownsBinding = claim is not null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk caller-to-agent binding compensation claim did not complete cleanly.");
            }
        }

        // Release this attempt's own deterministic agent leg and mixing bridge unconditionally. Track whether every
        // ARI effect genuinely succeeded (the client treats already-gone resources as success, so only a real outage
        // returns false); the durable record is retired only when cleanup fully succeeded, leaving a transient
        // failure for the reconciler to finish instead of orphaning it.
        var cleanedUp = true;

        if (!string.IsNullOrWhiteSpace(agentChannelId) &&
            !await TryHangupAsync(agentChannelId, cancellationToken))
        {
            cleanedUp = false;
        }

        if (!string.IsNullOrWhiteSpace(bridgeId) &&
            !await TryDestroyBridgeAsync(bridgeId, cancellationToken))
        {
            cleanedUp = false;
        }

        // The caller is ALWAYS returned to holding when the connect flow had already detached it, regardless of who
        // owns the agent leg: a Pending-disposition teardown deliberately leaves the caller alone, so the connect
        // flow is the single owner of re-parking it for re-offer. A re-park that could not even hang the caller up
        // leaves it possibly alive with no bridge, so it blocks record retirement — the durable CallerDetached
        // marker lets the reconciler retry the re-park on a later sweep instead of stranding the caller.
        if (!string.IsNullOrWhiteSpace(callerChannelIdToReturn) &&
            !await ReturnCallerToHoldingAsync(callerChannelIdToReturn, cancellationToken))
        {
            cleanedUp = false;
        }

        // Retire the durable record only when this flow owns the claim, every effect (ARI cleanup and caller
        // disposition) succeeded, AND the provisioning outcome was unambiguous — so a record is never deleted while a
        // resource it tracks is still live, a Terminating record owned by a terminal-event teardown is never deleted
        // out from under it, and a transport-ambiguous create whose "successful" 404 compensation does not prove the
        // resource is absent is left for the age-gated reconciler to re-probe and reclaim.
        if (ownsBinding && cleanedUp && !provisioningOutcomeAmbiguous && !string.IsNullOrWhiteSpace(agentChannelId))
        {
            try
            {
                await _channelTenantBindingStore.RemoveByChannelIdAsync(agentChannelId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk caller-to-agent binding compensation did not complete cleanly.");
            }
        }
    }

    private async Task RetireProvisionalTransferClaimAsync(string channelId, bool channelConfirmedGone)
    {
        if (channelConfirmedGone)
        {
            // The destination channel is confirmed gone (a confirmed hangup, or an originate Asterisk definitely
            // rejected so no channel was created), so hard-delete the durable Joining claim and let a retry re-run.
            await _channelTenantBindingStore.RemoveByChannelIdAsync(channelId);

            return;
        }

        // The destination channel may still be live but its hangup was not confirmed. Retain the claim as a durable
        // Terminating record whose Joining pre-teardown disposition tells the teardown planner and reconciler to hang
        // up ONLY this dangling channel — never the shared canonical bridge or the customer — so the reconciler
        // reclaims the leaked channel once the provisioning lease elapses instead of deleting the only record that
        // could ever find it.
        await _channelTenantBindingStore.TryBeginTeardownAsync(channelId);
    }

    private async Task<bool> TryHangupAsync(string channelId, CancellationToken cancellationToken)
    {
        try
        {
            await _ariClient.HangupAsync(channelId, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk caller-to-agent channel compensation did not complete cleanly.");

            return false;
        }
    }

    private async Task<bool> TryDestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken)
    {
        try
        {
            await _ariClient.DestroyBridgeAsync(bridgeId, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(OperationalLogRedactor.RedactException(ex), "Asterisk caller-to-agent bridge compensation did not complete cleanly.");

            return false;
        }
    }

    private async Task<bool> ReturnCallerToHoldingAsync(string callerChannelId, CancellationToken cancellationToken)
    {
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + callerChannelId;
        var holdingBridgeCreated = false;

        try
        {
            await _ariClient.CreateBridgeAsync(holdingBridgeId, AsteriskAriConstants.HoldingBridgeType, cancellationToken);
            holdingBridgeCreated = true;

            await _ariClient.AddChannelToBridgeAsync(holdingBridgeId, callerChannelId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Re-parked Asterisk caller {CallerChannelId} into a holding bridge after a failed agent connect so the work can be re-offered.",
                    OperationalLogRedactor.Pseudonymize(callerChannelId, OperationalLogIdentifierCategory.Call));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                OperationalLogRedactor.RedactException(ex),
                "Asterisk could not re-park caller {CallerChannelId} after a failed agent connect; hanging up the caller to avoid a silent stranded channel.",
                OperationalLogRedactor.Pseudonymize(callerChannelId, OperationalLogIdentifierCategory.Call));

            // Destroy the holding bridge if it was created before the add failed (for example the caller vanished
            // before it could be parked). Otherwise the empty bridge leaks, since the caller is about to be hung up
            // and nothing else references it. Idempotent: the ARI client treats an already-gone bridge as success.
            if (holdingBridgeCreated)
            {
                try
                {
                    await _ariClient.DestroyBridgeAsync(holdingBridgeId, cancellationToken);
                }
                catch (Exception destroyEx)
                {
                    _logger.LogWarning(OperationalLogRedactor.RedactException(destroyEx), "Asterisk holding-bridge cleanup after a failed re-park did not complete cleanly.");
                }
            }

            try
            {
                await _ariClient.HangupAsync(callerChannelId, cancellationToken);
            }
            catch (Exception hangupEx)
            {
                _logger.LogWarning(OperationalLogRedactor.RedactException(hangupEx), "Asterisk caller hangup after a failed re-park did not complete cleanly.");

                // Neither re-park nor hangup succeeded, so the caller may still be alive with no bridge. Report the
                // failure so the connect flow retains the durable record and the reconciler retries the caller's
                // disposition on a later sweep rather than orphaning it.
                return false;
            }

            // The caller could not be re-parked but was hung up, so it is no longer stranded and the record may be
            // retired.
            return true;
        }
    }

    private async Task DetachFromHoldingBridgeAsync(string callerChannelId, CancellationToken cancellationToken)
    {
        var holdingBridgeId = AsteriskConstants.HoldingBridgePrefix + callerChannelId;

        try
        {
            await _ariClient.RemoveChannelFromBridgeAsync(holdingBridgeId, callerChannelId, cancellationToken);
            await _ariClient.DestroyBridgeAsync(holdingBridgeId, cancellationToken);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    OperationalLogRedactor.RedactException(ex),
                    "No Asterisk holding bridge to detach caller {CallerChannelId} from before bridging to the agent; continuing.",
                    OperationalLogRedactor.Pseudonymize(callerChannelId, OperationalLogIdentifierCategory.Call));
            }
        }
    }

    private async Task<string> ResolveAgentEndpointAsync(
        ContactCenterConnectRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.AgentEndpoint))
        {
            return request.AgentEndpoint.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.AgentUserId))
        {
            return null;
        }

        // The agent's reachable endpoint is their browser softphone, provisioned per registration as a random
        // PJSIP authorization user rather than a stable extension, so it must be resolved from the tenant-scoped
        // lease store instead of derived from the agent id. The newest live lease represents the agent's current
        // registration; when none exists the agent is not registered and the connect fails closed.
        var liveLeases = await _pjsipCredentialLeaseStore.ListLiveByUserAsync(
            request.AgentUserId.Trim(),
            _clock.UtcNow,
            cancellationToken);

        var activeLease = liveLeases
            .OrderByDescending(lease => lease.IssuedUtc)
            .FirstOrDefault();

        if (activeLease is null || string.IsNullOrWhiteSpace(activeLease.AuthorizationUser))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No live Asterisk softphone registration was found for the selected agent, so the caller cannot be connected.");
            }

            return null;
        }

        return string.Concat(
            AsteriskAriConstants.PjsipEndpointTechnology,
            "/",
            activeLease.AuthorizationUser.Trim());
    }

    private static string CreateStableConnectKey(ContactCenterConnectRequest request)
    {
        // Derive the stable key from the per-acceptance command id when present so each connect ATTEMPT produces
        // distinct ARI resource ids (bridge and agent channel). Retries of the same command stay idempotent (same
        // command id -> same ids, so a duplicated originate cannot double-bridge), but a re-offer after a failed
        // attempt is a NEW command, so a prior attempt's paused teardown can only ever destroy its own generation's
        // resources. That id uniqueness is itself the fence for the ABA hazard where a late teardown would
        // otherwise tear down a re-offered call's freshly created bridge that happened to reuse the interaction id.
        var baseKey = !string.IsNullOrWhiteSpace(request.InteractionId)
            ? request.InteractionId
            : request.ProviderCallId;

        if (request.Metadata is not null &&
            request.Metadata.TryGetValue(ContactCenterConstants.CommandMetadata.CommandId, out var commandId) &&
            !string.IsNullOrWhiteSpace(commandId))
        {
            return string.Concat(baseKey, "-", commandId);
        }

        return baseKey;
    }

    private static string CreateDeterministicAriId(string prefix, string value)
    {
        var builder = new StringBuilder(prefix);

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('-');
            }
        }

        return builder.ToString();
    }

    private static ContactCenterVoiceProviderResult Failure(string errorCode, string errorMessage)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
        };
    }
}
