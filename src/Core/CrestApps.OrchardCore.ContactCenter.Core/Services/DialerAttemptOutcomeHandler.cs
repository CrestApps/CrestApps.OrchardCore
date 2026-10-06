using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Dispositions every dialer attempt that ended before an agent was connected, so the dialer -- not the agent it
/// reserved -- deals with it.
/// </summary>
/// <remarks>
/// <para>
/// A paced call is placed before an agent is on it, so a call that rang out, was busy, rejected or failed, reached a
/// machine, or that the customer hung up on while the agent was being joined, never reached anybody. It used to be left
/// with the agent: reserved, popped on their screen, and -- once a customer had answered -- parked in wrap-up demanding a
/// disposition for a call they never heard. Now each one is completed here with the disposition for its outcome, as the
/// system, and the disposition's subject actions and the <c>ActivityDispositionApplied</c> event decide whether to call
/// again.
/// </para>
/// <para>
/// It runs on <c>DialerAttemptCompleted</c>, which every ended dialer call reports with its outcome, and on a
/// <c>DialFailed</c> the provider command reports when the provider refused to place the call at all. A number not in
/// service is completed by <see cref="DialerNotInServiceHandler"/> on the call's own end, so it is left to that.
/// </para>
/// </remarks>
public sealed class DialerAttemptOutcomeHandler : IContactCenterEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptOutcomeHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The scope's services. Completing the activity reaches the agent presence, queue and disposition services, which
    /// publish through the event publisher whose handlers this is, so they are resolved when an event is handled.
    /// </param>
    /// <param name="logger">The logger.</param>
    public DialerAttemptOutcomeHandler(
        IServiceProvider serviceProvider,
        ILogger<DialerAttemptOutcomeHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/DialerAttemptOutcome/v1";

    /// <inheritdoc/>
    /// <remarks>
    /// A redelivery finds the activity already completed and stops there.
    /// </remarks>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        var request = interactionEvent.EventType switch
        {
            ContactCenterConstants.Events.DialerAttemptCompleted => FromAttemptCompleted(interactionEvent),
            ContactCenterConstants.Events.DialFailed => FromRefusedDial(interactionEvent),
            _ => null,
        };

        if (request is null)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Dialer attempt for activity '{ActivityId}' (interaction '{InteractionId}') ended as {Outcome} before an agent was connected; dispositioning it without an agent.",
                request.ActivityItemId.SanitizeLogValue(),
                request.InteractionId.SanitizeLogValue(),
                request.Outcome);
        }

        var result = await _serviceProvider.GetRequiredService<IDialerAttemptFinalizer>().FinalizeAsync(request, cancellationToken);

        // A dial the provider refused was marked failed before the dialer dispositioned refusals; with nothing to
        // disposition it, it still ends that way rather than waiting in Dialing for the recovery sweep.
        if (result == DialerAttemptFinalizationResult.NotDispositioned &&
            interactionEvent.EventType == ContactCenterConstants.Events.DialFailed)
        {
            await _serviceProvider.GetRequiredService<IContactCenterActivityWriter>().ScheduleUpdateAsync(
                request.ActivityItemId,
                activity =>
                {
                    activity.Status = ActivityStatus.Failed;
                    activity.TerminalReasonCode = DialerTerminalReasons.Failed;
                },
                cancellationToken);
        }
    }

    private static DialerAttemptFinalizationRequest FromAttemptCompleted(InteractionEvent interactionEvent)
    {
        var data = interactionEvent.GetData<CallLifecycleEventData>();

        if (data is null ||
            string.IsNullOrEmpty(data.ActivityItemId) ||
            !DialerAttemptOutcomes.IsPreConnect(data.Outcome) ||
            string.Equals(data.Outcome, DialerAttemptOutcomes.NotInService, StringComparison.Ordinal) ||
            !IsCampaignDial(data))
        {
            return null;
        }

        return new DialerAttemptFinalizationRequest
        {
            ActivityItemId = data.ActivityItemId,
            InteractionId = data.InteractionId ?? interactionEvent.InteractionId,
            Outcome = data.Outcome,
            PhoneNumber = data.PhoneNumber,
            Detail = DescribeCause(data),
            Trigger = ContactCenterConstants.Events.DialerAttemptCompleted,
        };
    }

    // The dial command reports a dial the provider refused with the command that carried it and no call; a call that
    // was placed and ended unanswered is reported by its own end, through DialerAttemptCompleted.
    private static DialerAttemptFinalizationRequest FromRefusedDial(InteractionEvent interactionEvent)
    {
        var data = interactionEvent.GetData<CallLifecycleEventData>();

        if (data is null ||
            string.IsNullOrEmpty(data.ActivityItemId) ||
            !string.IsNullOrEmpty(data.CallSessionId) ||
            !data.Details.ContainsKey("commandId") ||
            !data.Details.TryGetValue("dialerProfileId", out var profileId) ||
            string.IsNullOrEmpty(profileId) ||
            QueueCallbackDialerProfile.IsCallbackProfile(profileId))
        {
            return null;
        }

        return new DialerAttemptFinalizationRequest
        {
            ActivityItemId = data.ActivityItemId,
            InteractionId = data.InteractionId ?? interactionEvent.InteractionId,
            Outcome = DialerAttemptOutcomes.Failed,
            PhoneNumber = data.PhoneNumber,
            Detail = string.IsNullOrWhiteSpace(data.Reason) ? "the provider refused the call" : $"the provider refused the call: {data.Reason}",
            Trigger = ContactCenterConstants.Events.DialFailed,
        };
    }

    // Only a call a campaign profile placed is dispositioned here; a queued callback keeps its own retry.
    private static bool IsCampaignDial(CallLifecycleEventData data)
        => data.Details.TryGetValue("dialerProfileId", out var profileId) &&
            !string.IsNullOrEmpty(profileId) &&
            !QueueCallbackDialerProfile.IsCallbackProfile(profileId);

    private static string DescribeCause(CallLifecycleEventData data)
    {
        var cause = data.ProviderHangupCause?.Trim();
        var sipCause = data.SipHangupCause?.Trim();

        if (string.IsNullOrEmpty(sipCause))
        {
            return string.IsNullOrEmpty(cause) ? null : cause;
        }

        return string.IsNullOrEmpty(cause)
            ? $"SIP {sipCause}"
            : $"{cause} (SIP {sipCause})";
    }
}
