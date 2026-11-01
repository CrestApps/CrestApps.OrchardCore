using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A worker's portal inbox.
/// </summary>
public interface IWorkforceInbox
{
    /// <summary>
    /// Lists a worker's notifications, newest first.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of notifications, how many are unread, and one page of them.</returns>
    Task<(int Count, int Unread, IReadOnlyList<WorkforceInboxItem> Items)> PageAsync(string workerId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all of a worker's notifications read.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were marked.</returns>
    Task<int> MarkAllReadAsync(string workerId, CancellationToken cancellationToken = default);
}
