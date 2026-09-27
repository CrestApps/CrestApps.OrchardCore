using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;

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

        if (reason == "supervisor-unreachable")
        {
            _logger.LogWarning(
                "Supervisor '{SupervisorUserId}' never connected to the phone call '{CallId}': their phone did not answer leg '{SupervisorLegId}' (hangup cause {HangupCause}). The {Mode} engagement ended.",
                engagement.SupervisorUserId.SanitizeLogValue(),
                engagement.CallId.SanitizeLogValue(),
                supervisorLegId.SanitizeLogValue(),
                hangupCause,
                engagement.Mode);
        }

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

    // Records the call a supervisor took over, on the leg they took it on, as a call of their own: the same party, the same
    // direction, answered. Their soft phone lists it, and the leg's events keep it current. Returns whether it was recorded.
    private async Task<bool> RecordSupervisorsCallAsync(PhoneCallEngagement engagement, string takeOverLegId)
    {
        if (_telephonyInteractions is null || string.IsNullOrWhiteSpace(takeOverLegId))
        {
            return false;
        }

        try
        {
            var agentsCall = await _telephonyInteractions.FindByCallIdAsync(engagement.MonitoredUserId, engagement.CallId, CancellationToken.None);
            var supervisor = await _agentProfileManager.FindByUserIdAsync(engagement.SupervisorUserId, CancellationToken.None);

            await _telephonyInteractions.CreateAsync(new TelephonyInteraction
            {
                InteractionId = IdGenerator.GenerateId(),
                CallId = takeOverLegId,
                ProviderName = engagement.ProviderName ?? agentsCall?.ProviderName,
                UserId = engagement.SupervisorUserId,
                UserName = supervisor?.UserName ?? supervisor?.DisplayName,
                From = agentsCall?.From,
                To = agentsCall?.To,
                Direction = agentsCall?.Direction ?? CallDirection.Outbound,
                IsExtension = agentsCall?.IsExtension ?? false,
                ExtensionNumber = agentsCall?.ExtensionNumber,
                Outcome = CallOutcome.InProgress,
                StartedUtc = _clock.UtcNow,
                AwaitingAnswer = false,
            }, CancellationToken.None);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Recorded the phone call '{CallId}' supervisor '{SupervisorUserId}' took over from user '{MonitoredUserId}' as their own call on leg '{TakeOverLegId}'.",
                    engagement.CallId.SanitizeLogValue(),
                    engagement.SupervisorUserId.SanitizeLogValue(),
                    engagement.MonitoredUserId.SanitizeLogValue(),
                    takeOverLegId.SanitizeLogValue());
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The call is the supervisor's on the provider either way; without the record their phone only shows the banner.
            _logger.LogError(
                ex,
                "The phone call '{CallId}' supervisor '{SupervisorUserId}' took over could not be recorded as theirs; their soft phone will not list it.",
                engagement.CallId.SanitizeLogValue(),
                engagement.SupervisorUserId.SanitizeLogValue());

            return false;
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
