using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Hears about every punch after it is saved, including a correction or a removal, such as to keep the worker's day
/// in Time Tracker.
/// </summary>
public interface ITimePunchObserver
{
    /// <summary>
    /// Called after a punch is saved.
    /// </summary>
    /// <param name="punch">The punch saved.</param>
    /// <param name="replaced">The punch it corrects or removes, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task PunchedAsync(TimePunch punch, TimePunch replaced, CancellationToken cancellationToken = default);
}
