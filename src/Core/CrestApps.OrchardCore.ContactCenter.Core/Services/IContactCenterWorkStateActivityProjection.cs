using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Projects Contact Center work state onto the CRM activity so CRM listing, filtering, and reporting stay
/// readable. The projection runs after the routing scope commits and never participates in a routing
/// transaction, so a CRM edit that conflicts with it can never fail a reservation.
/// </summary>
public interface IContactCenterWorkStateActivityProjection
{
    /// <summary>
    /// Reconciles the CRM activity with the current work state of that activity.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProjectAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds a work state document that does not exist yet from the routing fields the CRM activity already
    /// carries, so work that predates this feature is not reported as unassigned with no attempts.
    /// </summary>
    /// <param name="workState">The work state to seed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> when a CRM activity was found and the work state was seeded from it; otherwise, <c>false</c>.</returns>
    Task<bool> TrySeedAsync(ContactCenterWorkState workState, CancellationToken cancellationToken = default);
}
