using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for Contact Center work state.
/// </summary>
public interface IContactCenterWorkStateManager : ICatalogManager<ContactCenterWorkState>
{
    /// <summary>
    /// Finds the work state that belongs to the specified CRM activity.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work state, or <see langword="null"/> when the activity has never been routed.</returns>
    Task<ContactCenterWorkState> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the work state documents that belong to the specified CRM activities.
    /// </summary>
    /// <param name="activityItemIds">The CRM activity identifiers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work state documents that exist for the requested activities.</returns>
    Task<IReadOnlyCollection<ContactCenterWorkState>> GetByActivityIdsAsync(
        IEnumerable<string> activityItemIds,
        CancellationToken cancellationToken = default);
}
