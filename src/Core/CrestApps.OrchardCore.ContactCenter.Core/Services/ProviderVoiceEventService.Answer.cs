using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What an answered outbound call does next: connect its agent, or -- when the call was screened for answering
/// machines -- wait to hear who answered, and hang up on a machine.
/// </summary>
public sealed partial class ProviderVoiceEventService
{
    /// <summary>
    /// Whether the provider said a machine, rather than a person, answered the call.
    /// </summary>
    private static bool IsMachineAnswered(CallSession session)
        => session.Metadata.TryGetValue(ContactCenterConstants.TelephonyMetadata.AnswerClassification, out var classification) &&
            classification is nameof(AnswerClassification.Machine) or nameof(AnswerClassification.Fax);

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

        // A call a machine answered, and that the platform hung up for it, ends like any answered call. It is
        // recorded as the machine answer it was, so nobody counts it as a conversation or an agent's call.
        if (hangupCause == HangupCause.NormalClearing && IsMachineAnswered(session))
        {
            hangupCause = HangupCause.AnsweringMachine;
        }

        session.HangupCause = hangupCause;
    }

    private async Task StageAnsweredOutboundBridgeAsync(
        CallSession session,
        Interaction interaction,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (session.Direction != InteractionDirection.Outbound)
        {
            return;
        }

        // A call an over-dialing Predictive profile placed has no agent yet: one is claimed once a person answers.
        var overDialed = string.IsNullOrEmpty(session.AgentId) &&
            string.IsNullOrEmpty(interaction.AgentId) &&
            DialerCallMetadata.IsOverDialed(interaction);

        if (string.IsNullOrEmpty(session.AgentId) && !overDialed)
        {
            return;
        }

        // The moment a person is known to be on the line: the answer itself, or the verdict when the call is screened.
        // The two seconds an agent has to reach them before the call counts as abandoned run from here.
        var liveAnsweredUtc = session.AnsweredUtc ?? now;

        // A call screened for answering machines connects its agent only once the provider has said who answered: a
        // few seconds of silence for the person, and no agent ever sitting through a voicemail greeting. The verdict
        // arrives as a second connected delivery, which comes back through here.
        if (session.Metadata.ContainsKey(ContactCenterConstants.TelephonyMetadata.AnswerDetectionRequested))
        {
            liveAnsweredUtc = now;

            if (!session.Metadata.TryGetValue(ContactCenterConstants.TelephonyMetadata.AnswerClassification, out var classification))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Holding the agent of answered outbound call '{ProviderCallId}' (interaction '{InteractionId}') until the provider says whether a person or a machine answered.",
                        session.ProviderCallId.SanitizeLogValue(),
                        interaction.ItemId.SanitizeLogValue());
                }

                return;
            }

            if (IsMachineAnswered(session))
            {
                ScheduleMachineHangup(session, interaction, classification);

                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Outbound call '{ProviderCallId}' (interaction '{InteractionId}') was answered by {AnswerClassification}; connecting the agent.",
                    session.ProviderCallId.SanitizeLogValue(),
                    interaction.ItemId.SanitizeLogValue(),
                    classification.SanitizeLogValue());
            }
        }

        // A person answered an automated dialer call: it now counts toward the profile's abandonment rate, whatever
        // happens next.
        if (await _abandonmentTracker.RecordLiveAnswerAsync(interaction, liveAnsweredUtc, cancellationToken))
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        if (overDialed)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Over-dialed call '{ProviderCallId}' (interaction '{InteractionId}') was answered by a person; claiming a free agent for it.",
                    session.ProviderCallId.SanitizeLogValue(),
                    interaction.ItemId.SanitizeLogValue());
            }

            ScheduleOverDialConnect(interaction);

            return;
        }

        if (!ProviderJoinsAgentAfterAnswer(session))
        {
            // A provider that does not join the agent through a leg of its own puts the agent on the call as it is
            // answered: the answer is the moment the agent is connected.
            if (DialerCallMetadata.MarkAgentJoined(interaction, session.AnsweredUtc ?? _clock.UtcNow))
            {
                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
            }

            return;
        }

        // The answer command that bridges the agent's soft-phone leg is only dispatchable when it carries the
        // agent's user id (call-control authorization is keyed on the user, not the agent record). The session
        // knows only the agent id, so resolve the user here; without it the bridge command would be refused at
        // dispatch and compensated, which strands the answered outbound call in dead air and drops the agent
        // straight into wrap-up.
        var agent = await _agentManager.FindByIdAsync(session.AgentId, cancellationToken);

        if (agent is null || string.IsNullOrWhiteSpace(agent.UserId))
        {
            _logger.LogWarning(
                "Skipped bridging answered outbound call '{ProviderCallId}' for interaction '{InteractionId}' because agent '{AgentId}' could not be resolved to a user to authorize the connect.",
                session.ProviderCallId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue(),
                session.AgentId.SanitizeLogValue());

            // Nobody can be connected to the person who answered. An automated dialer call tells them who called
            // and ends, rather than leaving them on a silent line.
            if (DialerCallMetadata.IsCampaignDial(interaction))
            {
                var messageStarted = await _abandonmentTracker.AbandonAsync(interaction, session.ProviderName, session.ProviderCallId, DialerAbandonment.Reasons.AgentUnavailable, cancellationToken);

                await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

                if (!messageStarted)
                {
                    ScheduleHangup(session, interaction, "no agent could be connected to it");
                }
            }

            return;
        }

        var commandId = await AnsweredCallBridge.RegisterAsync(
            _providerCommandStateService,
            _callSessionManager,
            session,
            interaction,
            session.AgentId,
            agent.UserId,
            reservationId: null,
            cancellationToken);

        _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
            processor.DispatchAsync(commandId, CancellationToken.None));
    }

    // An over-dialed call has no agent until a person answers. Once this delivery has committed the answer, the connector
    // claims a free agent and connects them, or plays the abandoned-call message when nobody is free. It runs after the
    // commit so the live answer the two-second rule is measured from is on record first.
    private void ScheduleOverDialConnect(Interaction interaction)
    {
        var interactionId = interaction.ItemId;

        _scopeExecutor.ScheduleAfterCommit<IServiceProvider>(async services =>
        {
            var connector = services.GetService<IPredictiveAgentConnector>();

            if (connector is null)
            {
                _logger.LogWarning(
                    "Over-dialed call '{InteractionId}' was answered, but no predictive connector is registered to connect an agent to it.",
                    interactionId.SanitizeLogValue());

                return;
            }

            await connector.ConnectAsync(interactionId, CancellationToken.None);
        });
    }

    // The call is hung up after this delivery commits, so the verdict is on record before the hangup it causes. The
    // hangup then comes back as the call's own end, recorded as a machine answer with the agent sent straight back to
    // ready rather than into wrap-up, and the activity is put back for a later attempt.
    private void ScheduleMachineHangup(CallSession session, Interaction interaction, string classification)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Outbound call '{ProviderCallId}' (interaction '{InteractionId}', activity '{ActivityId}') was answered by {AnswerClassification}; hanging up without connecting the agent.",
                session.ProviderCallId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue(),
                interaction.ActivityItemId.SanitizeLogValue(),
                classification.SanitizeLogValue());
        }

        ScheduleHangup(session, interaction, "it was answered by a machine");
    }

    // Hangs up the answered call once this delivery commits, so what caused the hangup is on record before the hangup
    // comes back as the call's own end.
    private void ScheduleHangup(CallSession session, Interaction interaction, string why)
    {
        var providerName = session.ProviderName;
        var providerCallId = session.ProviderCallId;
        var interactionId = interaction.ItemId;

        _scopeExecutor.ScheduleAfterCommit<ITelephonyProviderResolver>(async resolver =>
        {
            if (await resolver.GetAsync(providerName) is not ITelephonyCallControlProvider callControl)
            {
                _logger.LogWarning(
                    "Could not hang up call '{ProviderCallId}' after {Why} because provider '{ProviderName}' cannot control calls.",
                    providerCallId.SanitizeLogValue(),
                    why,
                    providerName.SanitizeLogValue());

                return;
            }

            var result = await callControl.HangupAsync(new CallReference { CallId = providerCallId }, CancellationToken.None);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "The provider did not confirm hanging up call '{ProviderCallId}' (interaction '{InteractionId}') after {Why}: {Error}.",
                    providerCallId.SanitizeLogValue(),
                    interactionId.SanitizeLogValue(),
                    why,
                    result.Error.SanitizeLogValue());
            }
        });
    }
}
