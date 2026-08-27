using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Contributes optional dialer profiles and queueing behavior to Omnichannel activity management.
/// </summary>
public interface IActivityDialerContributor
{
    /// <summary>
    /// Gets the available dialer profiles.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The available profile descriptors.</returns>
    Task<IEnumerable<ActivityDialerProfileDescriptor>> GetProfilesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a dialer profile by identifier.
    /// </summary>
    /// <param name="profile">The resolved profile descriptor.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching profile descriptor, or <see langword="null"/>.</returns>
    Task<ActivityDialerProfileDescriptor> FindByIdAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues an activity for outbound dialing under the given campaign, tagging it with the profile that
    /// dials it. The routing target is derived from the campaign, so activities loaded for the same campaign
    /// share one queue regardless of which profile dials them.
    /// </summary>
    /// <param name="activityId">The activity identifier.</param>
    /// <param name="campaignId">The campaign the inventory was loaded for.</param>
    /// <param name="profile">The resolved profile descriptor that dials the activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EnqueueAsync(
        string activityId,
        string campaignId,
        ActivityDialerProfileDescriptor profile,
        CancellationToken cancellationToken = default);
}
