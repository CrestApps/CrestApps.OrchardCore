using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for activity reservations.
/// </summary>
public interface IActivityReservationStore : ICatalog<ActivityReservation>
{
    /// <summary>
    /// Lists, oldest expiry first, up to <paramref name="maxResults"/> pending reservations that have passed
    /// their expiration time, starting just after the supplied keyset cursor. Ordering is stable (expiry then
    /// document id) and paging is keyset based rather than offset based, so a caller may drain the backlog in
    /// pages that stay correct even while reservations are concurrently expired or created.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="afterExpiresUtc">The expiry component of the keyset cursor; results start strictly after it. Pass <see langword="null"/> for the first page.</param>
    /// <param name="afterDocumentId">The document identifier component of the keyset cursor, used to break ties among reservations sharing <paramref name="afterExpiresUtc"/>. Ignored when <paramref name="afterExpiresUtc"/> is <see langword="null"/>.</param>
    /// <param name="maxResults">The maximum number of expired reservations to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A page of expired pending reservations together with the cursor for the next page.</returns>
    Task<ExpiredReservationPage> GetExpiredAsync(
        DateTime utcNow,
        DateTime? afterExpiresUtc,
        long afterDocumentId,
        int maxResults,
        CancellationToken cancellationToken = default);

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
    Task<IReadOnlyCollection<ActivityReservation>> GetActiveByAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the non-terminal (pending or accepted) reservations bound to the specified activity.
    /// </summary>
    /// <param name="activityItemId">The activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pending and accepted reservations for the activity.</returns>
    Task<IReadOnlyCollection<ActivityReservation>> GetActiveByActivityAsync(string activityItemId, CancellationToken cancellationToken = default);
}
