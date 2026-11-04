using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Hears about a time-off request after a change to it is saved: submitted, approved, declined, canceled or given a
/// place from the waitlist. Unlike <see cref="ITimeOffApprovalHandler"/>, it runs outside the change, so it may save
/// its own work.
/// </summary>
public interface ITimeOffRequestObserver
{
    /// <summary>
    /// Called after a request is saved.
    /// </summary>
    /// <param name="request">The request as saved.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(TimeOffRequest request, CancellationToken cancellationToken = default);
}
