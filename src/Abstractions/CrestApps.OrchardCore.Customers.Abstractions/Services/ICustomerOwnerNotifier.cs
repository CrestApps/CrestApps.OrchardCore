using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Sends a buyer a message the same way from every commerce feature. A buyer with an account is reached through the
/// notification system, so the message honors their channel preference; a guest, who has no account, is reached by
/// email at the contact <see cref="ICustomerContactResolver"/> resolves for them.
/// </summary>
public interface ICustomerOwnerNotifier
{
    /// <summary>
    /// Sends <paramref name="message"/> to <paramref name="owner"/>.
    /// </summary>
    /// <param name="owner">The buyer to reach.</param>
    /// <param name="guestContact">The contact captured for a guest at purchase time; ignored for a buyer with an account.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether the message reached the buyer through at least one channel.</returns>
    Task<bool> NotifyAsync(CustomerOwner owner, ICustomerContact guestContact, CustomerOwnerMessage message, CancellationToken cancellationToken = default);
}
