using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Says what limits a worker's shifts keep to, for the auto-scheduler and the checks on claims and trades.
/// </summary>
public interface IWorkerScheduleLimitsProvider
{
    /// <summary>
    /// Returns a worker's limits on a team.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The limits.</returns>
    Task<WorkerScheduleLimits> GetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default);
}
