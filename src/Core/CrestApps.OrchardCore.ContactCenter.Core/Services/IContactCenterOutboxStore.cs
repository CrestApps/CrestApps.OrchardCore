using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for Contact Center outbox messages.
/// </summary>
public interface IContactCenterOutboxStore : ICatalog<ContactCenterOutboxMessage>
{
    /// <summary>
    /// Finds the outbox message associated with an event.
    /// </summary>
    /// <param name="eventId">The interaction event identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching message, or <see langword="null"/> when none exists.</returns>
    Task<ContactCenterOutboxMessage> FindByEventIdAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the pending messages whose next attempt time is at or before the supplied time, oldest first.
    /// </summary>
    /// <param name="nowUtc">The current UTC time used to select due messages.</param>
    /// <param name="maxCount">The maximum number of messages to return in one batch.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due outbox messages.</returns>
    Task<IReadOnlyCollection<ContactCenterOutboxMessage>> ListDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default);
}
