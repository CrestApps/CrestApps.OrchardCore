using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IQueueLimitService"/>.
/// </summary>
public sealed class QueueLimitService : IQueueLimitService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityQueueService _queueService;
    private readonly IWaitingCallVoicemailSink _voicemailSink;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueLimitService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="queueManager">The queue manager, used to validate overflow targets.</param>
    /// <param name="queueService">The queue service that moves a caller between queues.</param>
    /// <param name="voicemailSink">The sink that moves a waiting caller into voicemail.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public QueueLimitService(
        IQueueItemManager queueItemManager,
        IActivityQueueManager queueManager,
        IActivityQueueService queueService,
        IWaitingCallVoicemailSink voicemailSink,
        IClock clock,
        ILogger<QueueLimitService> logger)
    {
        _queueItemManager = queueItemManager;
        _queueManager = queueManager;
        _queueService = queueService;
        _voicemailSink = voicemailSink;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QueueAdmissionDecision> AdmitAsync(ActivityQueue queue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (queue.MaxQueueSize <= 0 || queue.QueueFullAction == QueueMaxWaitAction.None)
        {
            return QueueAdmissionDecision.Admit(queue.ItemId);
        }

        var waiting = await _queueItemManager.CountWaitingAsync(queue.ItemId, cancellationToken);

        if (waiting < queue.MaxQueueSize)
        {
            return QueueAdmissionDecision.Admit(queue.ItemId);
        }

        switch (queue.QueueFullAction)
        {
            case QueueMaxWaitAction.Voicemail:
                return QueueAdmissionDecision.SendToVoicemail();

            case QueueMaxWaitAction.Overflow:
                var target = await FindOverflowQueueWithRoomAsync(queue, cancellationToken);

                if (target is not null)
                {
                    return QueueAdmissionDecision.Overflow(target);
                }

                // Every overflow queue is missing, disabled, or as full as this one. The caller is admitted
                // anyway: a size limit exists to spread load, not to hang up on people because of a
                // configuration gap, and the maximum-wait action still applies to them once they are in.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Queue '{QueueId}' is full and configured to overflow, but no overflow queue can take the caller; the caller was admitted over the limit.",
                        queue.ItemId.SanitizeLogValue());
                }

                return QueueAdmissionDecision.Admit(queue.ItemId);

            default:
                return QueueAdmissionDecision.Admit(queue.ItemId);
        }
    }

    /// <inheritdoc/>
    public async Task<int> EnforceMaxWaitAsync(ActivityQueue queue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);

        // This runs every few seconds against every queue, so a queue that sets no limit must not cost a read
        // of everybody waiting in it.
        if (queue.MaxWaitSeconds <= 0 || queue.MaxWaitAction == QueueMaxWaitAction.None)
        {
            return 0;
        }

        var waiting = await _queueItemManager.GetWaitingAsync(queue.ItemId, cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var applied = 0;

        foreach (var item in waiting)
        {
            // The wait is measured from when the caller entered this queue, which is what the queue promised
            // to cap. A caller who overflowed in from elsewhere starts this queue's clock afresh, the same way
            // its overflow thresholds do.
            var enteredUtc = item.QueueEnteredUtc == default
                ? item.EnqueuedUtc
                : item.QueueEnteredUtc;

            if ((now - enteredUtc).TotalSeconds < queue.MaxWaitSeconds)
            {
                continue;
            }

            if (await ApplyMaxWaitActionAsync(item, queue, cancellationToken))
            {
                applied++;
            }
        }

        return applied;
    }

    private async Task<bool> ApplyMaxWaitActionAsync(QueueItem item, ActivityQueue queue, CancellationToken cancellationToken)
    {
        switch (queue.MaxWaitAction)
        {
            case QueueMaxWaitAction.Overflow:
                // The wait threshold on each hop exists to give this queue a chance to answer, and it has had
                // that chance, so the first hop the caller has not been through applies immediately.
                var target = OverflowScheduler.SelectFirstEligibleTarget(item, queue);

                if (string.IsNullOrEmpty(target))
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning(
                            "Queue item '{ItemId}' has waited past the maximum for queue '{QueueId}', which is configured to overflow but names no overflow queue the caller has not already been through; the caller keeps waiting.",
                            item.ItemId.SanitizeLogValue(),
                            queue.ItemId.SanitizeLogValue());
                    }

                    return false;
                }

                await _queueService.OverflowItemAsync(item, queue, target, cancellationToken);

                return true;

            case QueueMaxWaitAction.Voicemail:
                var moved = await _voicemailSink.SendToVoicemailAsync(
                    item,
                    ContactCenterConstants.QueueLimits.MaxWaitVoicemailReasonCode,
                    cancellationToken);

                if (!moved && _logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Queue item '{ItemId}' has waited past the maximum for queue '{QueueId}', which is configured for voicemail, but no voice feature could move the caller; the caller keeps waiting.",
                        item.ItemId.SanitizeLogValue(),
                        queue.ItemId.SanitizeLogValue());
                }

                return moved;

            default:
                return false;
        }
    }

    /// <summary>
    /// Walks the overflow chain in order and returns the first queue that exists, is enabled, and has room for
    /// one more caller under its own size limit, so a full queue does not hand its overflow to another full one.
    /// </summary>
    private async Task<string> FindOverflowQueueWithRoomAsync(ActivityQueue queue, CancellationToken cancellationToken)
    {
        foreach (var target in OverflowScheduler.GetOrderedTargets(queue))
        {
            if (string.Equals(target.QueueId, queue.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var overflowQueue = await _queueManager.FindByIdAsync(target.QueueId, cancellationToken);

            if (overflowQueue is null || !overflowQueue.Enabled)
            {
                continue;
            }

            if (overflowQueue.MaxQueueSize > 0 &&
                overflowQueue.QueueFullAction != QueueMaxWaitAction.None &&
                await _queueItemManager.CountWaitingAsync(overflowQueue.ItemId, cancellationToken) >= overflowQueue.MaxQueueSize)
            {
                continue;
            }

            return overflowQueue.ItemId;
        }

        return null;
    }
}
