using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IQueuedDialerWorkGate"/>.
/// </summary>
public sealed class QueuedDialerWorkGate : IQueuedDialerWorkGate
{
    private readonly IDialerProfileReader _profileReader;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueService _queueService;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedDialerWorkGate"/> class.
    /// </summary>
    /// <param name="profileReader">The reader of the dialer profile a record was queued with.</param>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="interactionManager">The interaction manager, read for when the last attempt ended.</param>
    /// <param name="workStateService">The work state that holds the record's attempt count.</param>
    /// <param name="queueItemManager">The queue item manager, used to send a record that is not due to the back.</param>
    /// <param name="queueService">The queue service, used to take out a record that has no attempts left.</param>
    /// <param name="scopeExecutor">The executor that completes an exhausted record once routing has committed.</param>
    /// <param name="logger">The logger.</param>
    public QueuedDialerWorkGate(
        IDialerProfileReader profileReader,
        IOmnichannelActivityManager activityManager,
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IQueueItemManager queueItemManager,
        IActivityQueueService queueService,
        IContactCenterScopeExecutor scopeExecutor,
        ILogger<QueuedDialerWorkGate> logger)
    {
        _profileReader = profileReader;
        _activityManager = activityManager;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _queueItemManager = queueItemManager;
        _queueService = queueService;
        _scopeExecutor = scopeExecutor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> TryHoldBackAsync(QueueItem queueItem, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        if (queueItem.Status != QueueItemStatus.Waiting ||
            string.IsNullOrEmpty(queueItem.ActivityItemId) ||
            string.IsNullOrEmpty(queueItem.DialerProfileId) ||
            QueueCallbackDialerProfile.IsCallbackProfile(queueItem.DialerProfileId))
        {
            return false;
        }

        var activity = await _activityManager.FindByIdAsync(queueItem.ActivityItemId, cancellationToken);

        if (activity is null || activity.Status.IsTerminal())
        {
            return false;
        }

        var profile = await _profileReader.FindByIdAsync(queueItem.DialerProfileId, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        // A turned-off profile places no calls. Its records used to be offered to agents anyway -- the pacer skips a
        // disabled profile, but agent-driven (Preview and Manual) inventory is offered by routing, which never looked.
        // They wait at the back of the queue, untouched, until the profile is turned back on.
        if (DialerWorkHoldEvaluator.EvaluateProfile(profile) == DispatchBlocker.ProfileDisabled)
        {
            await SendToBackAsync(queueItem, utcNow, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Held back campaign record '{ActivityId}' in queue '{QueueId}' without reserving an agent: dialer profile '{Profile}' is turned off.",
                    activity.ItemId.SanitizeLogValue(),
                    queueItem.QueueId.SanitizeLogValue(),
                    profile.Name);
            }

            return true;
        }

        var workState = await _workStateService.GetAsync(activity.ItemId, cancellationToken);
        var nextAttempt = ContactCenterWorkState.NextAttemptNumber(workState, activity.Attempts);
        var lastInteraction = await _interactionManager.FindByActivityIdAsync(activity.ItemId, cancellationToken);

        var lastOutcome = lastInteraction is null ? null : DialerCallMetadata.GetOutcome(lastInteraction);
        var (blocker, dueUtc) = DialerWorkHoldEvaluator.EvaluateRecord(
            profile,
            activity,
            nextAttempt,
            lastInteraction?.EndedUtc,
            string.Equals(lastOutcome, DialerAttemptOutcomes.Answered, StringComparison.Ordinal),
            utcNow);

        if (blocker == DispatchBlocker.AttemptsExhausted)
        {
            return await TakeOutExhaustedAsync(queueItem, activity, nextAttempt, profile, cancellationToken);
        }

        if (dueUtc is not { } due)
        {
            return false;
        }

        await SendToBackAsync(queueItem, utcNow, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Held back campaign record '{ActivityId}' in queue '{QueueId}' without reserving an agent: it is not due until {DueUtc:O}.",
                activity.ItemId.SanitizeLogValue(),
                queueItem.QueueId.SanitizeLogValue(),
                due);
        }

        return true;
    }

    private async Task SendToBackAsync(QueueItem queueItem, DateTime utcNow, CancellationToken cancellationToken)
    {
        queueItem.EnqueuedUtc = utcNow;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
    }

    private async Task<bool> TakeOutExhaustedAsync(
        QueueItem queueItem,
        OmnichannelActivity activity,
        int nextAttempt,
        DialerProfile profile,
        CancellationToken cancellationToken)
    {
        queueItem.ReservationId = null;
        queueItem.AgentId = null;
        await _queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Took campaign record '{ActivityId}' out of queue '{QueueId}' without reserving an agent: attempt {AttemptNumber} would exceed the {MaxAttempts} attempts allowed (dialer profile '{Profile}', or the subject's own maximum).",
                activity.ItemId.SanitizeLogValue(),
                queueItem.QueueId.SanitizeLogValue(),
                nextAttempt,
                DialerAttemptLimit.Resolve(activity, profile),
                profile.Name);
        }

        // Completing the activity runs its subject actions and publishes; it waits until routing has committed.
        var activityItemId = activity.ItemId;

        if (!_scopeExecutor.ScheduleAfterCommit<IServiceProvider>(services => DialerAttemptFinalizer.FinalizeExhaustedAsync(services, activityItemId)))
        {
            await _scopeExecutor.ExecuteAsync(services => DialerAttemptFinalizer.FinalizeExhaustedAsync(services, activityItemId));
        }

        return true;
    }
}
