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
        ActivityDialerProfileDescriptor profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.RoutingTargetId);

        await _activityQueueService.EnqueueAsync(
            activityId,
            profile.RoutingTargetId,
            priority: null,
            cancellationToken);
    }

    private static ActivityDialerProfileDescriptor CreateDescriptor(DialerProfile profile)
    {
        return new ActivityDialerProfileDescriptor
        {
            ProfileId = profile.ItemId,
            DisplayName = profile.Name ?? profile.ItemId,
            ActivitySource = DialerActivitySourceHelper.GetActivitySource(profile.Mode),
            CampaignId = profile.CampaignId,
            RoutingTargetId = profile.QueueId,
        };
    }
}
