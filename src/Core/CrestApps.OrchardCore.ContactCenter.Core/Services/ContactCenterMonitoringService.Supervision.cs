using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Changing an engaged supervisor's mode, and what the supervisor's own clients are told about their engagement.
/// </summary>
public sealed partial class ContactCenterMonitoringService
{
    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> SwitchModeAsync(
        string interactionId,
        string supervisorId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return SupervisorEngagementResult.Failure("An interaction is required.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return SupervisorEngagementResult.Failure("The interaction could not be found.");
        }

        // A supervisor already listening could otherwise start talking to the customer in the middle of the capture.
        if (interaction.RecordingState == RecordingState.Paused)
        {
            return SupervisorEngagementResult.Failure("A sensitive-data capture is in progress on this interaction. Monitoring is unavailable until it completes.");
        }

        var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Principal = principal,
            UserId = supervisorId,
            Verb = CallControlVerb.SupervisorEngage,
            InteractionId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            SupervisorOperation = true,
        }, cancellationToken);

        if (!authorization.Succeeded)
        {
            return SupervisorEngagementResult.Failure(authorization.FailureReason);
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is null || !provider.Capabilities.HasFlag(ResolveCapability(mode)))
        {
            return SupervisorEngagementResult.Failure($"The voice provider does not support the '{mode}' engagement.");
        }

        var callSession = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var live = callSession?.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
            string.Equals(monitorSession.SupervisorUserId, supervisorId, StringComparison.Ordinal));

        if (live is null)
        {
            return SupervisorEngagementResult.Failure("You are not listening to this call.");
        }

        if (live.Mode == mode)
        {
            return SupervisorEngagementResult.Success();
        }

        if (provider is not IContactCenterVoiceSupervisorInterventionProvider interventionProvider)
        {
            // Without a way to change the role on the supervisor's leg, the engagement is started again in the new mode.
            var stopped = await StopEngagementAsync(interaction.ItemId, supervisorId, principal, live.Mode, cancellationToken);

            return stopped.Succeeded
                ? await EngageAsync(interaction.ItemId, supervisorId, principal, mode, cancellationToken)
                : stopped;
        }

        var previousMode = live.Mode;

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                interventionProvider.SwitchModeAsync(new ContactCenterVoiceMonitoringRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = authorization.ProviderCallId ?? interaction.ProviderInteractionId,
                    SupervisorId = supervisorId,
                    Mode = mode,
                    AgentLegId = FindAgentLegId(callSession),
                    SupervisorLegId = live.ProviderLegId,
                }, commandCancellationToken));

            if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
            {
                return SupervisorEngagementResult.Failure(
                    providerResult?.ErrorMessage ?? $"The voice provider did not confirm the change to '{mode}'.");
            }
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown($"The voice provider did not confirm the change to '{mode}' before the server timeout.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown($"The change to '{mode}' was interrupted before the provider outcome could be confirmed.");
        }

        var now = _clock.UtcNow;

        // Written onto a fresh copy: the provider's leg events are recorded on the call while it is being asked.
        await _callSessionUpdater.UpdateAsync(interaction.ItemId, current =>
        {
            var engaged = current.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
                string.Equals(monitorSession.SupervisorUserId, supervisorId, StringComparison.Ordinal));

            if (engaged is null)
            {
                return false;
            }

            engaged.Mode = mode;

            // A barging supervisor is a party of the conversation; listening and whispering are not.
            if (!string.IsNullOrEmpty(engaged.ProviderLegId))
            {
                if (mode == MonitorMode.Barge)
                {
                    CallTopologyProjector.EnsureBridge(current, current.Bridge?.ProviderBridgeId, now);
                    CallTopologyProjector.Join(current, engaged.ProviderLegId, CallPartyRole.Supervisor, now, engaged.SupervisorAgentId);
                }
                else
                {
                    CallTopologyProjector.Leave(current, engaged.ProviderLegId, now);
                }
            }

            return true;
        }, cancellationToken);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.SupervisorMonitorModeChanged,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = supervisorId,
            SourceComponent = ContactCenterConstants.Components.RealTime,
        };

        interactionEvent.SetData(new Dictionary<string, string>
        {
            ["mode"] = mode.ToString(),
            ["previousMode"] = previousMode.ToString(),
            ["supervisorId"] = supervisorId,
        });

        await _publisher.PublishAsync(interactionEvent, CancellationToken.None);
        await NotifyAsync(SupervisorEngagementNotification.ModeChanged, interaction, callSession, supervisorId, mode, monitorToken: null, reason: null);

        return SupervisorEngagementResult.Success();
    }

    // The agent's own leg on the call: the one a whispering supervisor is heard by and a takeover releases.
    internal static string FindAgentLegId(CallSession callSession)
        => callSession?.Legs
            .LastOrDefault(leg =>
                leg is not null &&
                leg.Role == CallPartyRole.Agent &&
                leg.AnsweredUtc.HasValue &&
                !leg.EndedUtc.HasValue &&
                !string.IsNullOrEmpty(leg.ProviderLegId) &&
                !string.Equals(leg.ProviderLegId, callSession.ProviderCallId, StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(callSession.AgentId) || string.IsNullOrEmpty(leg.AgentId) || string.Equals(leg.AgentId, callSession.AgentId, StringComparison.Ordinal)))
            ?.ProviderLegId;

    private static string FindSupervisorLegId(CallSession callSession, string supervisorId)
        => callSession?.ActiveMonitorSessions
            .FirstOrDefault(monitorSession => string.Equals(monitorSession.SupervisorUserId, supervisorId, StringComparison.Ordinal))
            ?.ProviderLegId;

    private async Task NotifyAsync(
        string state,
        Interaction interaction,
        CallSession callSession,
        string supervisorId,
        MonitorMode mode,
        string monitorToken,
        string reason)
    {
        if (_notifier is null || string.IsNullOrEmpty(supervisorId))
        {
            return;
        }

        var agentId = callSession?.AgentId ?? interaction?.AgentId;
        string agentName = null;

        if (_agentProfileManager is not null && !string.IsNullOrEmpty(agentId))
        {
            var agent = await _agentProfileManager.FindByIdAsync(agentId, CancellationToken.None);
            agentName = string.IsNullOrWhiteSpace(agent?.DisplayName) ? agent?.Name ?? agent?.UserName : agent.DisplayName;
        }

        await _notifier.NotifyEngagementAsync(new SupervisorEngagementNotification
        {
            State = state,
            InteractionId = interaction?.ItemId,
            SupervisorUserId = supervisorId,
            AgentId = agentId,
            AgentName = agentName,
            Mode = mode.ToString(),
            MonitorToken = monitorToken,
            Reason = reason,
            ServerTimeUtc = _clock.UtcNow,
        }, CancellationToken.None);
    }
}
