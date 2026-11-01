using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Says how long a worker worked, for policies that accrue per hour worked. The built-in source counts the paid time
/// of published shifts; a time clock can replace it with worked time.
/// </summary>
public interface IWorkedTimeSource
{
    /// <summary>
    /// Returns the minutes a worker worked on the days of a range, in their time zone.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The minutes.</returns>
    Task<int> GetWorkedMinutesAsync(WorkerProfile worker, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
