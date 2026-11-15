namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Finds a requester's manager, for approval steps that the requester's manager decides, such as from the requester's
/// staff record.
/// </summary>
public interface ITicketRequesterManagerSource
{
    /// <summary>
    /// Finds the user who manages a user.
    /// </summary>
    /// <param name="userId">The requester's user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The manager's user, or <see langword="null"/> when this source doesn't know one.</returns>
    Task<string> FindManagerUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
