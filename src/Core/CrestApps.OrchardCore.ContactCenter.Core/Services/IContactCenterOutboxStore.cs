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

    /// <summary>
    /// Counts the messages currently in the supplied dispatch state.
    /// </summary>
    /// <param name="status">The dispatch state to count.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages in the supplied state.</returns>
    Task<int> CountByStatusAsync(OutboxMessageStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the pending or claimed messages whose next attempt is already due at or before the supplied time.
    /// A sustained non-zero result indicates the dispatcher is not keeping up with the backlog.
    /// </summary>
    /// <param name="nowUtc">The current UTC time used to select overdue messages.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of overdue messages.</returns>
    Task<int> CountOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
