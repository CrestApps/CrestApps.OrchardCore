using System.Globalization;
using System.Linq;
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
    IContactCenterVoiceRecordingProvider
{
    private readonly ITelephonyProviderResolver _telephonyResolver;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IAsteriskAriClient _ariClient;
    private readonly IAsteriskChannelTenantBindingStore _channelTenantBindingStore;
    private readonly IAsteriskPjsipCredentialLeaseStore _pjsipCredentialLeaseStore;
    private readonly IAsteriskAgentChannelReadySignal _agentChannelReadySignal;
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
            ContactCenterVoiceProviderCapabilities.Recording;

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

        var result = await provider.DialAsync(new DialRequest
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
                RecordingState.Stopped or RecordingState.None => await StopRecordingAsync(recordingName, cancellationToken),
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

            var outcomeUnknown = IsRecordingOutcomeUnknown(ex);

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
        string recordingName,
        CancellationToken cancellationToken)
    {
        var stored = await _ariClient.StopBridgeRecordingAsync(recordingName, cancellationToken);
        var format = string.IsNullOrWhiteSpace(stored?.Format)
            ? AsteriskAriConstants.RecordingFormat
            : stored.Format;

        return RecordingSuccess(recordingName, format, stored?.Duration);
    }

    private static ContactCenterVoiceProviderResult RecordingSuccess(
        string recordingName,
        string format,
        int? durationSeconds)
    {
        var metadata = new Dictionary<string, string>
        {
            [ContactCenterConstants.RecordingMetadata.RecordingName] = recordingName,
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

    private static bool IsRecordingOutcomeUnknown(AsteriskAriException exception)
    {
        // Per CC-2, a recording state change is unknown only when no ARI response was ever observed because the
        // request timed out or Asterisk was unreachable in transit; the client surfaces that as a null-status
        // exception that wraps the underlying transport failure. A received HTTP error response (4xx or 5xx) is a
        // confirmed failure, and a null-status exception with no inner transport failure is a definite local
        // pre-flight rejection (the provider is unconfigured or the tenant does not own the ARI application) that
        // never reached Asterisk, so neither of those is ambiguous.
        return exception.StatusCode is null && exception.InnerException is not null;
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
