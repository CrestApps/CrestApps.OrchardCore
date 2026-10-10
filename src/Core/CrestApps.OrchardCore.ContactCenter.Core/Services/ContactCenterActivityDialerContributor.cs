using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Contributes Contact Center dialer profiles and queueing to Omnichannel activity management.
/// </summary>
public sealed class ContactCenterActivityDialerContributor : IActivityDialerContributor
{
    private readonly IDialerProfileManager _dialerProfileManager;
    private readonly IActivityQueueService _activityQueueService;
    private readonly IQueueItemManager _queueItemManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityDialerContributor"/> class.
    /// </summary>
    /// <param name="dialerProfileManager">The dialer profile manager.</param>
    /// <param name="activityQueueService">The activity queue service.</param>
    /// <param name="queueItemManager">The queue item manager, which reads and re-tags waiting campaign records.</param>
    public ContactCenterActivityDialerContributor(
        IDialerProfileManager dialerProfileManager,
        IActivityQueueService activityQueueService,
        IQueueItemManager queueItemManager)
    {
        _dialerProfileManager = dialerProfileManager;
        _activityQueueService = activityQueueService;
        _queueItemManager = queueItemManager;
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
