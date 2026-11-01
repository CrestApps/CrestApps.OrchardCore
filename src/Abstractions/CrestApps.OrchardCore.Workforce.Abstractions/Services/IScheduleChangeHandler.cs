using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Hears about every change recorded to a published schedule, such as to record predictability pay.
/// </summary>
public interface IScheduleChangeHandler
{
    /// <summary>
    /// Handles a recorded change.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RecordedAsync(ScheduleChange change, CancellationToken cancellationToken = default);
}
