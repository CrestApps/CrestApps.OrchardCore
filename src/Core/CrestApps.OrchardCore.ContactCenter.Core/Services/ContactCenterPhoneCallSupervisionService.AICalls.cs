using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Listening to a call the AI voice agent is holding.
/// </summary>
/// <remarks>
/// An AI call has no agent leg and no Contact Center session while the AI talks: it is one customer leg. The
/// engagement is recorded like an agent's own phone call, keyed by <see cref="AICallUserMarker"/> in place of the
/// agent, and the provider's supervisor leg is pointed at the customer leg in monitor mode, so the supervisor hears
/// the conversation without being heard. The supervisor's phone answers the leg and stops it as it does any other
/// engagement.
/// </remarks>
public sealed partial class ContactCenterPhoneCallSupervisionService
{
    /// <summary>
    /// Stands in for the monitored user in the key of an engagement on an AI call.
    /// </summary>
    public const string AICallUserMarker = "ai-voice-agent";

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> ListenToAICallAsync(
        string activityId,
        string providerName,
        string customerLegId,
        string supervisorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(customerLegId) || string.IsNullOrEmpty(supervisorUserId))
        {
            return SupervisorEngagementResult.Failure("The call could not be found.");
        }

        if (_voiceProviderResolver.Get(providerName) is not IContactCenterVoiceMonitoringProvider monitoring)
        {
            return SupervisorEngagementResult.Failure("The call's provider cannot be listened to.");
        }

        // A listen left behind by a call that ended without telling us is replaced rather than blocking a new one.
        var existing = await _engagements.FindAsync(activityId, supervisorUserId, cancellationToken);

        if (existing is not null)
        {
            await _engagements.RemoveAsync(existing, cancellationToken);
        }

        var engagement = new PhoneCallEngagement
        {
            CallId = activityId,
            ProviderName = providerName,
            MonitoredUserId = AICallUserMarker,
            SupervisorUserId = supervisorUserId,
            Mode = MonitorMode.Monitor,
            MonitorToken = Guid.NewGuid().ToString("N"),
            ProviderCallId = customerLegId,
            AgentLegId = customerLegId,
            StartedUtc = _clock.UtcNow,
        };

        await _engagements.SaveAsync(engagement, cancellationToken);
        await NotifyAsync(SupervisorEngagementNotification.Requested, engagement, agent: null, reason: null);

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                monitoring.EngageAsync(Request(engagement, MonitorMode.Monitor), commandCancellationToken));
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown("The voice provider did not confirm the listen before the server timeout; the provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown("The listen was interrupted before the provider outcome could be confirmed.");
        }

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            var failure = result?.ErrorMessage ?? "The voice provider did not confirm the listen.";

            await _engagements.RemoveAsync(engagement, cancellationToken);
            await NotifyAsync(SupervisorEngagementNotification.Ended, engagement, agent: null, failure);

            return SupervisorEngagementResult.Failure(failure);
        }

        engagement.SupervisorLegId = result.ProviderLegId;
        await _engagements.SaveAsync(engagement, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Supervisor '{SupervisorUserId}' is listening to AI call '{ActivityId}' on leg '{SupervisorLegId}'.",
                supervisorUserId.SanitizeLogValue(),
                activityId.SanitizeLogValue(),
                engagement.SupervisorLegId.SanitizeLogValue());
        }

        return SupervisorEngagementResult.Success();
    }
}
