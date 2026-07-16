using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ICallbackService"/>.
/// </summary>
public sealed class CallbackService : ICallbackService
{
    /// <summary>
    /// The maximum number of due callbacks promoted in one background pass.
    /// </summary>
    public const int MaxBatchSize = 100;

    private static readonly TimeSpan _promotionLease = TimeSpan.FromMinutes(5);

    private readonly ICallbackRequestManager _callbackManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IActivityQueueService _queueService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackService"/> class.
    /// </summary>
    /// <param name="callbackManager">The callback manager.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="queueService">The queue service used to enqueue promoted callbacks.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp callback times.</param>
    public CallbackService(
        ICallbackRequestManager callbackManager,
        IOmnichannelActivityManager activityManager,
        IActivityQueueService queueService,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _callbackManager = callbackManager;
        _activityManager = activityManager;
        _queueService = queueService;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<CallbackRequest> ScheduleAsync(CallbackRequest callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var now = _clock.UtcNow;

        if (callback.RequestedUtc == default)
        {
            callback.RequestedUtc = now;
        }

        if (callback.ScheduledUtc == default)
        {
            callback.ScheduledUtc = now;
        }

        callback.Status = CallbackRequestStatus.Pending;

        await _callbackManager.CreateAsync(callback, cancellationToken: cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.CallbackScheduled, callback, cancellationToken);

        return callback;
    }

    /// <inheritdoc/>
    public async Task<int> PromoteDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = await _callbackManager.ListDueAsync(now, MaxBatchSize, cancellationToken);
        var count = 0;

        foreach (var callback in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await TryClaimAsync(callback, cancellationToken))
            {
                continue;
            }

            var activity = await CreateActivityAsync(callback, cancellationToken);
            callback.ActivityItemId = activity.ItemId;
            callback.Status = CallbackRequestStatus.Scheduled;
            callback.Attempts++;
            callback.OwnerToken = null;
            callback.LeaseExpiresUtc = null;

            await _callbackManager.UpdateAsync(callback, cancellationToken: cancellationToken);

            if (!string.IsNullOrEmpty(callback.QueueId))
            {
                await _queueService.EnqueueAsync(activity.ItemId, callback.QueueId, priority: null, cancellationToken);
            }

            await PublishAsync(ContactCenterConstants.Events.CallbackPromoted, callback, cancellationToken);

            count++;
        }

        return count;
    }

    private async Task<bool> TryClaimAsync(CallbackRequest callback, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (callback.Status != CallbackRequestStatus.Pending ||
            (callback.LeaseExpiresUtc.HasValue && callback.LeaseExpiresUtc.Value > now))
        {
            return false;
        }

        callback.OwnerToken = Guid.NewGuid().ToString("N");
        callback.FenceToken++;
        callback.LeaseExpiresUtc = now.Add(_promotionLease);

        try
        {
            await _callbackManager.UpdateAsync(callback, cancellationToken: cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return false;
        }

        return true;
    }

    private async Task<OmnichannelActivity> CreateActivityAsync(CallbackRequest callback, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var activity = await _activityManager.NewAsync(cancellationToken: cancellationToken);
        activity.Kind = ActivityKind.Call;
        activity.Source = ActivitySources.Callback;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.PreferredDestination = callback.Destination;
        activity.CampaignId = callback.CampaignId;
        activity.ContactContentItemId = callback.ContactContentItemId;
        activity.ContactContentType = callback.ContactContentType;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        activity.Status = ActivityStatus.NotStated;
        activity.ScheduledUtc = now;
        activity.CreatedUtc = now;

        await _activityManager.CreateAsync(activity, cancellationToken: cancellationToken);

        return activity;
    }

    private Task PublishAsync(string eventType, CallbackRequest callback, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            AggregateType = nameof(CallbackRequest),
            AggregateId = callback.ItemId,
            SourceComponent = ContactCenterConstants.Components.Dialer,
        }, cancellationToken);
    }
}
