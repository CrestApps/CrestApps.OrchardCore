using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A way of reaching a worker, such as their portal inbox, push notifications or email. Other modules add their own,
/// such as SMS.
/// </summary>
public interface IWorkforceNotificationChannel
{
    /// <summary>
    /// Gets the channel's name, stored in a worker's turned-off channels.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the channel's name on screen.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets whether a worker can turn the channel off.
    /// </summary>
    bool CanBeTurnedOff => true;

    /// <summary>
    /// Delivers a notification to a worker. A channel that can't reach the worker does nothing.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SendAsync(WorkerProfile worker, WorkforceNotification notification, CancellationToken cancellationToken = default);
}
