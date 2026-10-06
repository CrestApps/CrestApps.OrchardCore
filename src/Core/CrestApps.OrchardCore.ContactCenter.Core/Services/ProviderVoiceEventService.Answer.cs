using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;

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
        CancellationToken cancellationToken)
    {
        if (session.Direction != InteractionDirection.Outbound || string.IsNullOrEmpty(session.AgentId))
        {
            return;
        }

        // A call screened for answering machines connects its agent only once the provider has said who answered: a
        // few seconds of silence for the person, and no agent ever sitting through a voicemail greeting. The verdict
        // arrives as a second connected delivery, which comes back through here.
        if (session.Metadata.ContainsKey(ContactCenterConstants.TelephonyMetadata.AnswerDetectionRequested))
        {
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
                AgentUserId = agent.UserId,
                QueueId = session.QueueId,
            }),
        }, cancellationToken);

        _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
            processor.DispatchAsync(commandId, CancellationToken.None));
    }

    // The call is hung up after this delivery commits, so the verdict is on record before the hangup it causes. The
    // hangup then comes back as the call's own end, recorded as a machine answer with the agent sent straight back to
    // ready rather than into wrap-up, and the activity is put back for a later attempt.
    private void ScheduleMachineHangup(CallSession session, Interaction interaction, string classification)
    {
        var providerName = session.ProviderName;
        var providerCallId = session.ProviderCallId;
        var interactionId = interaction.ItemId;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Outbound call '{ProviderCallId}' (interaction '{InteractionId}', activity '{ActivityId}') was answered by {AnswerClassification}; hanging up without connecting the agent.",
                providerCallId.SanitizeLogValue(),
                interactionId.SanitizeLogValue(),
                interaction.ActivityItemId.SanitizeLogValue(),
                classification.SanitizeLogValue());
        }

        _scopeExecutor.ScheduleAfterCommit<ITelephonyProviderResolver>(async resolver =>
        {
            if (await resolver.GetAsync(providerName) is not ITelephonyCallControlProvider callControl)
            {
                _logger.LogWarning(
                    "Could not hang up machine-answered call '{ProviderCallId}' because provider '{ProviderName}' cannot control calls.",
                    providerCallId.SanitizeLogValue(),
                    providerName.SanitizeLogValue());

                return;
            }

            var result = await callControl.HangupAsync(new CallReference { CallId = providerCallId }, CancellationToken.None);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "The provider did not confirm hanging up machine-answered call '{ProviderCallId}' (interaction '{InteractionId}'): {Error}.",
                    providerCallId.SanitizeLogValue(),
                    interactionId.SanitizeLogValue(),
                    result.Error.SanitizeLogValue());
            }
        });
    }
}
