using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What a supervisor's own leg on a phone call reports, and the end of the call itself.
/// </summary>
public sealed partial class ContactCenterPhoneCallSupervisionService : ISupervisorLegEventSink
{
    /// <inheritdoc/>
    public async Task<bool> OnAnsweredAsync(
        string providerName,
        string providerCallId,
        string supervisorLegId,
        CancellationToken cancellationToken = default)
    {
        var engagement = await _engagements.FindBySupervisorLegAsync(supervisorLegId, cancellationToken);

        if (engagement is null)
        {
            return false;
        }

        engagement.ConnectedUtc ??= _clock.UtcNow;
        await _engagements.SaveAsync(engagement, cancellationToken);
        await NotifyAsync(SupervisorEngagementNotification.Connected, engagement, agent: null, reason: null);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> OnEndedAsync(
        string providerName,
        string providerCallId,
        string supervisorLegId,
        DateTime? endedUtc,
        HangupCause? hangupCause,
        CancellationToken cancellationToken = default)
    {
        var engagement = await _engagements.FindBySupervisorLegAsync(supervisorLegId, cancellationToken);

        if (engagement is null)
        {
            return false;
        }

        // A supervisor who took the call over was the one on it: their hanging up ends it for the other party too.
        if (engagement.TookOver &&
            _voiceProviderResolver.Get(engagement.ProviderName) is IContactCenterVoicePhoneCallMonitoringProvider phoneCalls)
        {
            await phoneCalls.HangupPhoneCallLegAsync(engagement.OtherPartyLegId, cancellationToken);
        }

        await _engagements.RemoveAsync(engagement, cancellationToken);

        var reason = engagement.TookOver
            ? "call-ended"
            : engagement.ConnectedUtc.HasValue ? "supervisor-left" : "supervisor-unreachable";

        await NotifyAsync(SupervisorEngagementNotification.Ended, engagement, agent: null, reason);

        return true;
    }

    /// <inheritdoc/>
    public async Task ReleaseCallAsync(string callId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        foreach (var engagement in await _engagements.ListAsync(callId, cancellationToken))
        {
            // The agent's leg of a call a supervisor took over ends with the takeover; the call is the supervisor's now.
            if (engagement.TookOver)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(engagement.SupervisorLegId) &&
                _voiceProviderResolver.Get(engagement.ProviderName) is IContactCenterVoiceSupervisorInterventionProvider interventions)
            {
                await interventions.ReleaseSupervisorLegAsync(engagement.SupervisorLegId, cancellationToken);
            }

            await _engagements.RemoveAsync(engagement, cancellationToken);
            await NotifyAsync(SupervisorEngagementNotification.Ended, engagement, agent: null, "call-ended");

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The phone call '{CallId}' ended; supervisor '{SupervisorUserId}' was let go.",
                    callId.SanitizeLogValue(),
                    engagement.SupervisorUserId.SanitizeLogValue());
            }
        }
    }

    // The call a user is on as either party, while it lasts.
    private async Task<AgentPhoneCall> FindCallAsync(string userId, string callId, CancellationToken cancellationToken)
    {
        var calls = await FindCallsAsync([userId], cancellationToken);

        return calls.TryGetValue(userId, out var call) && string.Equals(call.CallId, callId, StringComparison.Ordinal)
            ? call
            : null;
    }

    private async Task<string> ResolveExtensionAsync(Dictionary<string, string> resolved, string number, CancellationToken cancellationToken)
    {
        if (_extensionResolver is null || string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        if (!resolved.TryGetValue(number, out var userId))
        {
            var resolution = await _extensionResolver.ResolveAsync(number, cancellationToken);
            userId = resolution?.Found == true ? resolution.UserId : null;
            resolved[number] = userId;
        }

        return userId;
    }

    private async Task NotifyAsync(string state, PhoneCallEngagement engagement, AgentProfile agent, string reason)
    {
        if (_notifier is null || engagement is null)
        {
            return;
        }

        if (agent is null && state == SupervisorEngagementNotification.Requested && !string.IsNullOrEmpty(engagement.MonitoredUserId))
        {
            agent = await _agentProfileManager.FindByUserIdAsync(engagement.MonitoredUserId, CancellationToken.None);
        }

        await _notifier.NotifyEngagementAsync(new SupervisorEngagementNotification
        {
            State = state,
            InteractionId = PhoneCallKey.Create(engagement.MonitoredUserId, engagement.CallId),
            SupervisorUserId = engagement.SupervisorUserId,
            AgentId = engagement.MonitoredAgentId,
            AgentName = agent is null ? null : string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.Name ?? agent.UserName : agent.DisplayName,
            Mode = engagement.Mode.ToString(),
            MonitorToken = state == SupervisorEngagementNotification.Requested ? engagement.MonitorToken : null,
            Reason = reason,
            ServerTimeUtc = _clock.UtcNow,
        }, CancellationToken.None);
    }
}
