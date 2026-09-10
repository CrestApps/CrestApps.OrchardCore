using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed partial class AsteriskContactCenterVoiceProvider
{
    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> EngageAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.ContactCenterVoiceWorkPartition);

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
                ex,
                "Asterisk could not probe the existing {MonitorMode} supervisor leg for interaction {InteractionId}; leaving any established engagement untouched.",
                request.Mode,
                request.InteractionId.SanitizeLogValue());

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
                    ex,
                    "Asterisk could not re-assert the {MonitorMode} supervisor topology for interaction {InteractionId}; leaving the existing engagement untouched.",
                    request.Mode,
                    request.InteractionId.SanitizeLogValue());

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
                ex,
                "Asterisk failed to start the {MonitorMode} supervisor engagement for interaction {InteractionId}; compensating supervisor-only side effects.",
                request.Mode,
                request.InteractionId.SanitizeLogValue());

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
                ex,
                "The {MonitorMode} supervisor engagement for interaction {InteractionId} failed unexpectedly; compensating supervisor-only side effects.",
                request.Mode,
                request.InteractionId.SanitizeLogValue());

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

        using var workLease = _workManager.TryEnter(AsteriskConstants.ContactCenterVoiceWorkPartition);

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
                ex,
                "Asterisk failed to stop the {MonitorMode} supervisor engagement for interaction {InteractionId}.",
                request.Mode,
                request.InteractionId.SanitizeLogValue());

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

        var readyOutcome = await OriginateSupervisorLegAsync(request.InteractionId, supervisorEndpoint, supervisorChannelId, cancellationToken);

        if (readyOutcome == AsteriskAgentChannelReadyOutcome.Canceled)
        {
            // Let the outer engagement catch compensate and rethrow, so a canceled engagement is never persisted as
            // a false supervisor no-answer.
            cancellationToken.ThrowIfCancellationRequested();

            throw new OperationCanceledException(cancellationToken);
        }

        if (readyOutcome != AsteriskAgentChannelReadyOutcome.Ready)
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

        var readyOutcome = await OriginateSupervisorLegAsync(request.InteractionId, supervisorEndpoint, supervisorChannelId, cancellationToken);

        if (readyOutcome == AsteriskAgentChannelReadyOutcome.Canceled)
        {
            // Let the outer engagement catch compensate and rethrow, so a canceled engagement is never persisted as
            // a false supervisor no-answer.
            cancellationToken.ThrowIfCancellationRequested();

            throw new OperationCanceledException(cancellationToken);
        }

        if (readyOutcome != AsteriskAgentChannelReadyOutcome.Ready)
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

    private async Task<AsteriskAgentChannelReadyOutcome> OriginateSupervisorLegAsync(
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
            _logger.LogWarning(ex, "Asterisk supervisor engagement compensation did not complete cleanly.");
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
}
