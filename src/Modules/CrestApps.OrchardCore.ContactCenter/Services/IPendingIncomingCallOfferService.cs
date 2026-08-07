using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Resolves the current ringing inbound offer for an agent so the soft-phone modal can be restored after
/// a refresh or reconnect.
/// </summary>
public interface IPendingIncomingCallOfferService
{
    /// <summary>
    /// Gets the current pending inbound offer for the specified user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pending inbound offer, or <see langword="null"/> when no ringing offer is active.</returns>
    Task<PendingIncomingCallOffer> GetForUserAsync(string userId, CancellationToken cancellationToken = default);
}
