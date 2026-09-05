using CrestApps.OrchardCore.ContactCenter.Core.Models;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityDialerContributor"/> class.
    /// </summary>
    /// <param name="dialerProfileManager">The dialer profile manager.</param>
    /// <param name="activityQueueService">The activity queue service.</param>
    public ContactCenterActivityDialerContributor(
        IDialerProfileManager dialerProfileManager,
        IActivityQueueService activityQueueService)
    {
        _dialerProfileManager = dialerProfileManager;
        _activityQueueService = activityQueueService;
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
        await _activityQueueService.EnqueueAsync(
            activityId,
            ContactCenterConstants.CampaignQueue.CreateId(campaignId),
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
