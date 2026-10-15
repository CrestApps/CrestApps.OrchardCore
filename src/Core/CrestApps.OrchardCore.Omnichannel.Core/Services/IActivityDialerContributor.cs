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
    /// Lists the activities waiting in the campaign's dialer queue, with the dialer profile each is queued under.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The waiting activities.</returns>
    Task<IReadOnlyCollection<ActivityDialerWaitingRecord>> GetWaitingRecordsAsync(
        string campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues an activity for outbound dialing under the given campaign, tagging it with the profile that
    /// dials it. The routing target is derived from the campaign, so activities loaded for the same campaign
    /// share one queue regardless of which profile dials them. An activity already waiting in that queue keeps
    /// its place and is tagged with the given profile instead.
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

    /// <summary>
    /// Moves an activity waiting in a campaign's dialer queue to another campaign's queue, keeping the dialer profile
    /// it is queued under. Work the dialer is dialing, or an agent already has, is left where it is.
    /// </summary>
    /// <param name="activityId">The activity identifier.</param>
    /// <param name="campaignId">The campaign whose queue the activity should wait in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What happened to the activity's place in the dialer.</returns>
    Task<ActivityDialerCampaignMoveResult> MoveToCampaignAsync(
        string activityId,
        string campaignId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the activities, of those given, that the dialer is working right now: being dialed, ringing an agent,
    /// or on a call an agent took. Changing such an activity under the dialer would pull it out from under a live call.
    /// </summary>
    /// <param name="activityIds">The activity identifiers to check.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifiers of the activities the dialer is working.</returns>
    Task<IReadOnlySet<string>> GetActivitiesInFlightAsync(
        IEnumerable<string> activityIds,
        CancellationToken cancellationToken = default);
}
