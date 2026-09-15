using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Writes CRM activity fields on behalf of Contact Center routing without enlisting the write in the routing
/// transaction, so a losing race against a concurrent CRM edit can never fail a routing
/// transition.
/// </summary>
public interface IContactCenterActivityWriter
{
    /// <summary>
    /// Schedules a CRM activity mutation to run after the current routing scope commits. When no shell scope
    /// is available the mutation is applied immediately instead.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="mutate">The mutation to apply.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ScheduleUpdateAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a CRM activity mutation and commits it, retrying in a fresh scope when a concurrent CRM edit
    /// wins the compare-and-set race.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="mutate">The mutation to apply.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task UpdateAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken = default);
}
