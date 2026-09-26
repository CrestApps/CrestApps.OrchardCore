using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// A supervisor taking a call over from its agent.
/// </summary>
/// <remarks>
/// <para>
/// The provider moves the media first: the supervisor becomes heard by the customer, then the agent's leg is released.
/// Only once it has confirmed both is the call handed over on the platform's side, reading the call again, since the
/// agent's leg ending is reported back while this runs. The agent's leg ends, the supervisor's leg becomes the call's
/// agent leg, and the interaction and its call are the supervisor's: the call's end, its talk time from here on and its
/// after-call work are theirs.
/// </para>
/// <para>
/// The released agent's part of the call ends the way it would at the end of a call: after-call work for queue-routed
/// work, ready again for a direct call. The supervisor becomes busy when they were ready for work, so routing does not
/// offer them another call while they talk. The report sees <see cref="ContactCenterConstants.Events.SupervisorTookOver"/>
/// close the agent's talk time and an agent-leg answer open the supervisor's.
/// </para>
/// </remarks>
public sealed partial class ContactCenterSupervisorInterventionService
{
    private const string TakeoverSource = AgentStateChangeSources.SupervisorTakeover;

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> TakeOverAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var call = await AuthorizeCallAsync(interactionId, supervisorUserId, principal, cancellationToken);

        if (call.Failure is not null)
        {
            return call.Failure;
        }

        var interaction = call.Interaction;

        if (interaction.RecordingState == RecordingState.Paused)
        {
            return SupervisorEngagementResult.Failure(SensitiveCaptureMessage);
        }

        // The call-control boundary resolves the supervisor's own agent profile; handling a call needs one.
        var supervisorAgentId = call.Authorization.AgentId;

        if (string.IsNullOrEmpty(supervisorAgentId))
        {
            return SupervisorEngagementResult.Failure("You need an agent profile to take a call over.");
        }

        var session = call.Authorization.CallSession ?? await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var engagement = session?.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
            string.Equals(monitorSession.SupervisorUserId, supervisorUserId, StringComparison.Ordinal));

        if (engagement is null || string.IsNullOrEmpty(engagement.ProviderLegId))
        {
            return SupervisorEngagementResult.Failure("Listen to, coach or join the call first: a call is taken over from being on it.");
        }

        if (!engagement.ConnectedUtc.HasValue)
        {
            return SupervisorEngagementResult.Failure("You are not connected to the call yet.");
        }

        if (string.Equals(session.AgentId, supervisorAgentId, StringComparison.Ordinal))
        {
            return SupervisorEngagementResult.Failure("You are already handling this call.");
        }

        var agentLegId = ContactCenterMonitoringService.FindAgentLegId(session);

        if (string.IsNullOrEmpty(agentLegId))
        {
            return SupervisorEngagementResult.Failure("The agent's leg of this call is not known, so it cannot be taken over.");
        }

        if (_voiceProviderResolver.Get(interaction.ProviderName) is not IContactCenterVoiceSupervisorInterventionProvider provider)
        {
            return SupervisorEngagementResult.Failure("The voice provider cannot hand a call to a supervisor.");
        }

        var supervisorLegId = engagement.ProviderLegId;

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                provider.TakeOverAsync(new ContactCenterVoiceMonitoringRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = call.Authorization.ProviderCallId ?? interaction.ProviderInteractionId,
                    SupervisorId = supervisorUserId,
                    Mode = MonitorMode.Barge,
                    AgentLegId = agentLegId,
                    SupervisorLegId = supervisorLegId,
                }, commandCancellationToken));

            if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
            {
                return SupervisorEngagementResult.Failure(providerResult?.ErrorMessage ?? "The voice provider did not confirm the takeover.");
            }
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown("The voice provider did not confirm the takeover before the server timeout.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown("The takeover was interrupted before the provider outcome could be confirmed.");
        }

        // Read again: the release of the agent's leg comes back as a provider event that may already be recorded.
        interaction = await _interactionManager.FindByIdAsync(interaction.ItemId, CancellationToken.None) ?? interaction;
        session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, CancellationToken.None) ?? session;

        var now = _clock.UtcNow;
        var previousAgentId = session.AgentId ?? interaction.AgentId;
        var mode = engagement.Mode;

        CallTopologyProjector.EndLeg(session, agentLegId, now, HangupCause.NormalClearing);

        // The engagement becomes the handling: the supervisor is no longer listening to the call, they are on it.
        CallTopologyProjector.EndMonitorSession(session, supervisorUserId, now);
        CallTopologyProjector.UpsertLeg(session, supervisorLegId, CallPartyRole.Agent, CallLegStatus.Answered, now, agentId: supervisorAgentId);
        CallTopologyProjector.EnsureBridge(session, session.Bridge?.ProviderBridgeId, now);
        CallTopologyProjector.Join(session, supervisorLegId, CallPartyRole.Agent, now, supervisorAgentId);

        session.AgentId = supervisorAgentId;
        interaction.AgentId = supervisorAgentId;

        await _callSessionManager.UpdateAsync(session, cancellationToken: CancellationToken.None);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: CancellationToken.None);

        var actor = ContactCenterActor.Supervisor(supervisorUserId);

        if (!string.IsNullOrEmpty(previousAgentId))
        {
            var released = new AgentStateChangeContext
            {
                Actor = actor,
                InteractionId = interaction.ItemId,
                ChangedUtc = now,
            };

            if (ContactCenterConstants.QueueStartsAfterCallWork(interaction.QueueId))
            {
                await _presenceManager.StartWrapUpAsync(previousAgentId, released, CancellationToken.None);
            }
            else
            {
                await _presenceManager.CompleteWorkAsync(previousAgentId, released, CancellationToken.None);
            }
        }

        await _presenceManager.StartConsultWorkAsync(supervisorAgentId, new AgentStateChangeContext
        {
            Actor = actor,
            Source = TakeoverSource,
            InteractionId = interaction.ItemId,
            ChangedUtc = now,
        }, CancellationToken.None);

        await RecordTakeoverAsync(interaction, session, previousAgentId, agentLegId, supervisorAgentId, supervisorLegId, supervisorUserId, mode, now);

        return SupervisorEngagementResult.Success();
    }

    private async Task RecordTakeoverAsync(
        Interaction interaction,
        CallSession session,
        string previousAgentId,
        string agentLegId,
        string supervisorAgentId,
        string supervisorLegId,
        string supervisorUserId,
        MonitorMode mode,
        DateTime now)
    {
        var actor = ContactCenterActor.Supervisor(supervisorUserId);

        // The released agent's part of the call ends here.
        var released = ContactCenterCallAudit.ForSession(session, interaction);
        released.AgentId = previousAgentId;
        released.ProviderLegId = agentLegId;
        released.LegRole = nameof(CallPartyRole.Agent);
        released.State = nameof(CallLegStatus.Ended);
        released.Reason = TakeoverSource;
        released.Target = supervisorAgentId;

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorTookOver,
            released,
            now,
            actor,
            $"supervisor-takeover:{interaction.ItemId}:{supervisorLegId}",
            CancellationToken.None);

        // ... and the supervisor's begins, on the leg they were already listening on.
        var answered = ContactCenterCallAudit.ForSession(session, interaction);
        answered.AgentId = supervisorAgentId;
        answered.ProviderLegId = supervisorLegId;
        answered.LegRole = nameof(CallPartyRole.Agent);
        answered.State = nameof(CallLegStatus.Answered);
        answered.Reason = TakeoverSource;

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegAnswered,
            answered,
            now,
            actor,
            $"agent-leg:{ContactCenterConstants.Events.AgentLegAnswered}:{supervisorLegId}",
            CancellationToken.None);

        var stopped = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.SupervisorMonitorStopped,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = supervisorUserId,
            SourceComponent = ContactCenterConstants.Components.RealTime,
        };

        stopped.SetData(new Dictionary<string, string>
        {
            ["mode"] = mode.ToString(),
            ["supervisorId"] = supervisorUserId,
            ["reason"] = "takeover",
        });

        await _publisher.PublishAsync(stopped, CancellationToken.None);

        if (_notifier is not null)
        {
            await _notifier.NotifyEngagementAsync(new SupervisorEngagementNotification
            {
                State = SupervisorEngagementNotification.TookOver,
                InteractionId = interaction.ItemId,
                SupervisorUserId = supervisorUserId,
                AgentId = previousAgentId,
                Mode = nameof(MonitorMode.Barge),
                ServerTimeUtc = now,
            }, CancellationToken.None);
        }
    }
}
