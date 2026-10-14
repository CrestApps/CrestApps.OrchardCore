using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Contributes Contact Center dialer profiles and queueing to Omnichannel activity management.
/// </summary>
public sealed class ContactCenterActivityDialerContributor : IActivityDialerContributor
{
    private readonly IDialerProfileManager _dialerProfileManager;
    private readonly IActivityQueueService _activityQueueService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly ContactCenterCoordinationOptions _coordinationOptions;

    // How many activity ids one in-flight query names, well inside every engine's parameter limit.
    private const int InFlightQueryBatchSize = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityDialerContributor"/> class.
    /// </summary>
    /// <param name="dialerProfileManager">The dialer profile manager.</param>
    /// <param name="activityQueueService">The activity queue service.</param>
    /// <param name="queueItemManager">The queue item manager, which reads and re-tags waiting campaign records.</param>
    /// <param name="session">The YesSql session, used to read the queue items of many activities at once.</param>
    /// <param name="distributedLock">The distributed lock, used to move a record while routing cannot offer it.</param>
    /// <param name="coordinationOptions">The coordination options that time the queue lock.</param>
    public ContactCenterActivityDialerContributor(
        IDialerProfileManager dialerProfileManager,
        IActivityQueueService activityQueueService,
        IQueueItemManager queueItemManager,
        ISession session,
        IDistributedLock distributedLock,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions)
    {
        _dialerProfileManager = dialerProfileManager;
        _activityQueueService = activityQueueService;
        _queueItemManager = queueItemManager;
        _session = session;
        _distributedLock = distributedLock;
        _coordinationOptions = coordinationOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ActivityDialerProfileDescriptor>> GetProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await _dialerProfileManager.GetAllAsync(cancellationToken);

        return profiles.Select(CreateDescriptor);
    }

    /// <inheritdoc/>
    public async Task<ActivityDialerProfileDescriptor> FindByIdAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var profile = await _dialerProfileManager.FindByIdAsync(profileId, cancellationToken);

        return profile is null ? null : CreateDescriptor(profile);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityDialerWaitingRecord>> GetWaitingRecordsAsync(
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        var items = await _queueItemManager.GetWaitingAsync(ContactCenterConstants.CampaignQueue.CreateId(campaignId), cancellationToken);

        return items
            .Where(item => !string.IsNullOrEmpty(item.ActivityItemId))
            .Select(item => new ActivityDialerWaitingRecord
            {
                ActivityId = item.ActivityItemId,
                ProfileId = item.DialerProfileId,
            })
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(
        string activityId,
        string campaignId,
        ActivityDialerProfileDescriptor profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        ArgumentNullException.ThrowIfNull(profile);

        // The routing target is the campaign's virtual queue, derived from the campaign the inventory was loaded
        // for — never from the profile. The profile id is stamped on the queue item so the pacer can apply its
        // settings while every activity for the campaign shares one queue.
        var queueId = ContactCenterConstants.CampaignQueue.CreateId(campaignId);

        // Queueing a record that is already waiting returns its queue item untouched, so a record moved to another
        // dialer profile kept being routed under the old one. It keeps its place in the queue and takes the new profile.
        var existing = await _queueItemManager.FindByActivityIdAsync(activityId, cancellationToken);

        if (existing is not null &&
            existing.Status == QueueItemStatus.Waiting &&
            string.Equals(existing.QueueId, queueId, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(existing.DialerProfileId, profile.ProfileId, StringComparison.Ordinal))
            {
                existing.DialerProfileId = profile.ProfileId;
                await _queueItemManager.UpdateAsync(existing, cancellationToken: cancellationToken);
            }

            return;
        }

        await _activityQueueService.EnqueueAsync(
            activityId,
            queueId,
            priority: null,
            dialerProfileId: profile.ProfileId,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ActivityDialerCampaignMoveResult> MoveToCampaignAsync(
        string activityId,
        string campaignId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        var targetQueueId = ContactCenterConstants.CampaignQueue.CreateId(campaignId);
        var existing = await _queueItemManager.FindByActivityIdAsync(activityId, cancellationToken);

        // Only campaign work moves: a record waiting in a real queue was routed there for its own reasons.
        if (existing is null || existing.IsSettled || !ContactCenterConstants.IsCampaignQueue(existing.QueueId))
        {
            return ActivityDialerCampaignMoveResult.NotQueued;
        }

        if (string.Equals(existing.QueueId, targetQueueId, StringComparison.OrdinalIgnoreCase))
        {
            return ActivityDialerCampaignMoveResult.AlreadyThere;
        }

        if (existing.Status != QueueItemStatus.Waiting)
        {
            return ActivityDialerCampaignMoveResult.InFlight;
        }

        // The old queue's assignment lock is the one routing holds while it picks and offers work, so the record
        // cannot be offered from the old campaign while it is being moved to the new one.
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            ActivityAssignmentService.GetQueueLockKey(existing.QueueId),
            _coordinationOptions.AssignmentLockTimeout,
            _coordinationOptions.AssignmentLockExpiration);

        if (!locked)
        {
            return ActivityDialerCampaignMoveResult.InFlight;
        }

        await using var acquiredLock = locker;

        // Read again under the lock: routing may have offered the record since it was first read.
        existing = await _queueItemManager.FindByIdAsync(existing.ItemId, cancellationToken);

        if (existing is null || existing.IsSettled)
        {
            return ActivityDialerCampaignMoveResult.NotQueued;
        }

        if (existing.Status != QueueItemStatus.Waiting)
        {
            return ActivityDialerCampaignMoveResult.InFlight;
        }

        // Taken out of the old campaign and queued afresh in the new one, so each campaign's queue reports its own wait.
        await _activityQueueService.DequeueAsync(existing, QueueItemStatus.Removed, cancellationToken);
        await _activityQueueService.EnqueueAsync(
            activityId,
            targetQueueId,
            existing.Priority,
            existing.DialerProfileId,
            cancellationToken);

        return ActivityDialerCampaignMoveResult.Moved;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetActivitiesInFlightAsync(
        IEnumerable<string> activityIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityIds);

        var inFlight = new HashSet<string>(StringComparer.Ordinal);

        foreach (var batch in activityIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Chunk(InFlightQueryBatchSize))
        {
            var items = await _session.QueryIndex<QueueItemIndex>(
                index => index.ActivityItemId.IsIn(batch) &&
                    (index.Status == QueueItemStatus.Reserved || index.Status == QueueItemStatus.Assigned),
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var item in items)
            {
                if (ContactCenterConstants.IsCampaignQueue(item.QueueId))
                {
                    inFlight.Add(item.ActivityItemId);
                }
            }
        }

        return inFlight;
    }

    private static ActivityDialerProfileDescriptor CreateDescriptor(DialerProfile profile)
    {
        return new ActivityDialerProfileDescriptor
        {
            ProfileId = profile.ItemId,
            DisplayName = profile.Name ?? profile.ItemId,
            ActivitySource = DialerActivitySourceHelper.GetActivitySource(profile.Mode),
        };
    }
}
