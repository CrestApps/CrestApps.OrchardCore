using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for activity reservations.
/// </summary>
public interface IActivityReservationStore : ICatalog<ActivityReservation>
{
    /// <summary>
    /// Lists the pending reservations that have passed their expiration time.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pending reservations that have expired.</returns>
    Task<IReadOnlyCollection<ActivityReservation>> ListExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the pending reservation currently held by the specified agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pending reservation, or <see langword="null"/> when none exists.</returns>
    Task<ActivityReservation> FindPendingByAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the non-terminal reservations currently bound to the specified agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pending and accepted reservations for the agent.</returns>
    Task<IReadOnlyCollection<ActivityReservation>> ListActiveByAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the non-terminal (pending or accepted) reservations bound to the specified activity.
    /// </summary>
    /// <param name="activityItemId">The activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pending and accepted reservations for the activity.</returns>
    Task<IReadOnlyCollection<ActivityReservation>> ListActiveByActivityAsync(string activityItemId, CancellationToken cancellationToken = default);
}
