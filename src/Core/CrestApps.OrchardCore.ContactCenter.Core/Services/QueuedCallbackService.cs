using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Takes a waiting caller out of the queue and schedules a callback in their place. The promise "press 1 and we
/// will call you" is only kept if the queue remembers when they arrived: a callback that costs the caller their
/// place is worse than waiting, because they hang up expecting to be treated as though they had held.
/// </summary>
public sealed class QueuedCallbackService : IQueuedCallbackService
{
    private readonly ICallbackService _callbackService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedCallbackService"/> class.
    /// </summary>
    public QueuedCallbackService(
        ICallbackService callbackService,
        IQueueItemManager queueItemManager,
        IClock clock,
        ILogger<QueuedCallbackService> logger)
    {
        _callbackService = callbackService;
        _queueItemManager = queueItemManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> AcceptAsync(QueueItem item, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Scheduling a callback with nowhere to call produces a job that can only ever fail, and the caller has
        // already hung up believing they will be phoned.
        if (string.IsNullOrWhiteSpace(destination))
        {
            return false;
        }

        // A caller who presses the key twice, or a redelivered DTMF event, must not produce two calls back.
        if (item.CallbackAcceptedUtc is not null || item.IsSettled)
        {
            return false;
        }

        var now = _clock.UtcNow;

        await _callbackService.ScheduleAsync(
            new CallbackRequest
            {
                ItemId = IdGenerator.GenerateId(),
                Destination = destination.Trim(),
                QueueId = item.QueueId,
                ActivityItemId = item.ActivityItemId,

                // The place in line is when the caller arrived, not when they pressed the key. Stamping "now"
                // would send someone who had already waited eight minutes to the back of the queue.
                RequestedUtc = item.QueueEnteredUtc,
                ScheduledUtc = now,
                Notes = "Requested from the queue, keeping the caller's place in line.",
            },
            cancellationToken);

        item.CallbackAcceptedUtc = now;

        // Leaving the item waiting would have an agent offered a caller who has already hung up.
        if (item.CanTransitionTo(QueueItemStatus.Removed))
        {
            item.TransitionTo(QueueItemStatus.Removed);
        }

        await _queueItemManager.UpdateAsync(item, cancellationToken: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Queued callback accepted for item '{ItemId}' on queue '{QueueId}', keeping the caller's place from {RequestedUtc:o}.",
                item.ItemId.SanitizeLogValue(),
                item.QueueId.SanitizeLogValue(),
                item.QueueEnteredUtc);
        }

        return true;
    }
}
