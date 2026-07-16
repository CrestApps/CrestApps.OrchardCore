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
    /// Enqueues an activity using the contributed dialer profile.
    /// </summary>
    /// <param name="activityId">The activity identifier.</param>
    /// <param name="profileId">The profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EnqueueAsync(
        string activityId,
        ActivityDialerProfileDescriptor profile,
        CancellationToken cancellationToken = default);
}
