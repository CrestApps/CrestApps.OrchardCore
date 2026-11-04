using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Works out the time zone a worker's or a team's days are in.
/// </summary>
public interface IWorkerTimeZoneService
{
    /// <summary>
    /// Gets a worker's time zone: their own, else their team's, else the site's.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="teamId">The team to use, or <see langword="null"/> for the worker's team.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The time zone identifier.</returns>
    Task<string> GetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a team's time zone: its own, else the site's.
    /// </summary>
    /// <param name="teamId">The team, or <see langword="null"/> for the site's.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The time zone identifier.</returns>
    Task<string> GetTeamAsync(string teamId, CancellationToken cancellationToken = default);
}
