using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A worker's private calendar feed of their published shifts and approved time off, to subscribe to in a calendar
/// app.
/// </summary>
public interface IWorkerFeedService
{
    /// <summary>
    /// Returns the secret of a worker's feed, making one the first time or a new one when asked.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="renew">Whether to replace the secret, which stops the old address working.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The secret.</returns>
    Task<string> GetTokenAsync(WorkerProfile worker, bool renew, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the feed for a secret: the worker's shifts and time off from four weeks ago to twelve weeks ahead.
    /// </summary>
    /// <param name="token">The secret.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The iCalendar text, or <see langword="null"/> when the secret matches no worker.</returns>
    Task<string> WriteAsync(string token, CancellationToken cancellationToken = default);
}
