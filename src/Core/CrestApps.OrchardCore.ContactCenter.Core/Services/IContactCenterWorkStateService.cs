using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the routing-facing entry point for reading and transitioning Contact Center work state.
/// </summary>
public interface IContactCenterWorkStateService
{
    /// <summary>
    /// Gets the work state for an activity without creating one.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work state, or <see langword="null"/> when the activity has never been routed.</returns>
    Task<ContactCenterWorkState> GetAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a routing transition to the work state of an activity, creating the work state on first use,
    /// and schedules the CRM projection to run after the current scope commits.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="mutate">The transition to apply.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The persisted work state, or <see langword="null"/> when no activity identifier was supplied.</returns>
    Task<ContactCenterWorkState> MutateAsync(
        string activityItemId,
        Action<ContactCenterWorkState> mutate,
        CancellationToken cancellationToken = default);
}
