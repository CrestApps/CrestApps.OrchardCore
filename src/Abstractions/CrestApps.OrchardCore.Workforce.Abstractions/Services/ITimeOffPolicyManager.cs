using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages time-off policies.
/// </summary>
public interface ITimeOffPolicyManager : ICatalogManager<TimeOffPolicy>
{
    /// <summary>
    /// Returns the enabled policies that apply to a worker on a day, one for each type: the worker's own assignment
    /// first, then the nearest of their teams'.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policies.</returns>
    Task<IReadOnlyList<TimeOffPolicy>> ListForWorkerAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the enabled policy of a type that applies to a worker on a day, or <see langword="null"/>.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="typeId">The time-off type.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policy.</returns>
    Task<TimeOffPolicy> FindForWorkerAsync(WorkerProfile worker, string typeId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the policies a worker or any of the given teams is assigned to, at any date.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="teamIds">The teams.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policies.</returns>
    Task<IReadOnlyList<TimeOffPolicy>> ListAssignedAsync(string workerId, IEnumerable<string> teamIds, CancellationToken cancellationToken = default);
}
