using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Completes the activity of a dialer call that found the number not in service, and marks the number, without an
/// agent.
/// </summary>
/// <remarks>
/// A dialed number that is not in service never reaches an agent, so nobody is there to disposition it: the call
/// used to end as if it had been abandoned, the activity was put back in the queue a minute later, and the same dead
/// number was dialed on every retry and every later load. The network says plainly when a number is not in service,
/// and the provider's hangup cause carries it; when it does, the attempt is completed here with the not-in-service
/// disposition, so the contact's history shows the dialer tried the number and found it dead, and the number is left
/// out from then on. This runs on the ended call's own event, before the recovery sweep that re-queues unanswered
/// attempts, which only picks up activities that are still in progress.
/// </remarks>
public sealed class DialerNotInServiceHandler : IContactCenterEventHandler
{
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerNotInServiceHandler"/> class.
    /// </summary>
    /// <param name="activityStore">The activity store used to load the dialed activity.</param>
    /// <param name="serviceProvider">
    /// The scope's services. Completing the activity reaches the agent presence and queue services, which publish
    /// through the event publisher whose handlers this is, so they are resolved when an event is handled rather
    /// than injected: injected, they closed a dependency cycle that stopped every handler from being built.
    /// </param>
    /// <param name="logger">The logger.</param>
    public DialerNotInServiceHandler(
        IOmnichannelActivityStore activityStore,
        IServiceProvider serviceProvider,
        ILogger<DialerNotInServiceHandler> logger)
    {
        _activityStore = activityStore;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/DialerNotInService/v1";

    /// <inheritdoc/>
    /// <remarks>
    /// A redelivery finds the activity already completed as not in service and stops there.
    /// </remarks>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.CallEnded)
        {
            return;
        }

        var data = interactionEvent.GetData<CallLifecycleEventData>();

        if (data is null ||
            !string.Equals(data.HangupCause, nameof(HangupCause.NotInService), StringComparison.Ordinal) ||
            !string.Equals(data.Direction, nameof(InteractionDirection.Outbound), StringComparison.Ordinal) ||
            string.IsNullOrEmpty(data.ActivityItemId))
        {
            return;
        }

        var activity = await _activityStore.FindByIdAsync(data.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        if (activity.Status.IsTerminal() &&
            string.Equals(activity.TerminalReasonCode, OmnichannelConstants.TerminalReasons.NumberNotInService, StringComparison.Ordinal))
        {
            return;
        }

        var interaction = string.IsNullOrEmpty(data.InteractionId)
            ? null
            : await _serviceProvider.GetRequiredService<IInteractionManager>().FindByIdAsync(data.InteractionId, cancellationToken);

        // The number the attempt dialed, which is not always the one on the activity now.
        var dialedNumber = interaction?.CustomerAddress;

        if (string.IsNullOrWhiteSpace(dialedNumber))
        {
            dialedNumber = activity.PreferredDestination;
        }

        var reason = DescribeCause(data);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Dialer call '{InteractionId}' for activity '{ActivityId}' found '{PhoneNumber}' not in service ({Reason}); completing the activity without an agent.",
                data.InteractionId.SanitizeLogValue(),
                activity.ItemId.SanitizeLogValue(),
                dialedNumber.SanitizeLogValue(),
                reason.SanitizeLogValue());
        }

        await _serviceProvider.GetRequiredService<INotInServiceActivityCompleter>().CompleteAsync(new NotInServiceCompletionRequest
        {
            Activity = activity,
            PhoneNumber = dialedNumber,
            Source = OmnichannelConstants.NotInServiceSources.Dialer,
            Reason = reason,
        }, cancellationToken);

        // The recovery sweep may already have put the attempt back in the queue before this event was handled. A
        // completed activity has nothing left to dial, so the waiting item goes too, or it would be offered again.
        var queueItem = await _serviceProvider.GetRequiredService<IQueueItemManager>().FindByActivityIdAsync(activity.ItemId, cancellationToken);

        if (queueItem is not null && queueItem.Status == QueueItemStatus.Waiting)
        {
            await _serviceProvider.GetRequiredService<IActivityQueueService>().DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Removed the re-queued attempt of activity '{ActivityId}' from queue '{QueueId}' because its number is not in service.",
                    activity.ItemId.SanitizeLogValue(),
                    queueItem.QueueId.SanitizeLogValue());
            }
        }
    }

    // What the provider said, in its own words, for the notes and the mark.
    private static string DescribeCause(CallLifecycleEventData data)
    {
        var cause = data.ProviderHangupCause?.Trim();
        var sipCause = data.SipHangupCause?.Trim();

        if (string.IsNullOrEmpty(sipCause))
        {
            return string.IsNullOrEmpty(cause) ? nameof(HangupCause.NotInService) : cause;
        }

        return string.IsNullOrEmpty(cause)
            ? $"SIP {sipCause}"
            : $"{cause} (SIP {sipCause})";
    }
}
