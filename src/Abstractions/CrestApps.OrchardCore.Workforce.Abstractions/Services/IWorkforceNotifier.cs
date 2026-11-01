using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Tells workers about what changed for them. Without the portal it does nothing; with it, every channel the worker
/// hasn't turned off delivers the notification.
/// </summary>
public interface IWorkforceNotifier
{
    /// <summary>
    /// Sends a notification to a worker.
    /// </summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task NotifyAsync(WorkforceNotification notification, CancellationToken cancellationToken = default);
}
