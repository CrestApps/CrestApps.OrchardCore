using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Hears when a worker's attendance points reach their policy's threshold.
/// </summary>
public interface IAttendancePointsHandler
{
    /// <summary>
    /// Called when a new exception brings a worker's points to the threshold or past it.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="points">The worker's points now.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ThresholdReachedAsync(WorkerProfile worker, AttendanceException exception, decimal points, TimeClockPolicy policy, CancellationToken cancellationToken = default);
}
