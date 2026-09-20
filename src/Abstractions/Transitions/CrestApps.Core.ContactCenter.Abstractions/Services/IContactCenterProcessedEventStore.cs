using CrestApps.Core.Services;
using CrestApps.Core.ContactCenter.Models;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Defines the persistence contract for event deduplication markers.
/// </summary>
public interface IContactCenterProcessedEventStore : ICatalog<ContactCenterProcessedEvent>
{
    /// <summary>
    /// Finds the marker recording that one handler has already processed one event.
    /// </summary>
    /// <remarks>
    /// The pair is what deduplication asks about, so the lookup belongs on the store rather than being
    /// composed by the caller out of a session and an index. A store backed by something other than YesSql
    /// answers the same question its own way.
    /// </remarks>
    /// <param name="handlerId">The handler the marker belongs to.</param>
    /// <param name="eventId">The event the handler processed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The marker, or <see langword="null"/> when the handler has not processed the event.</returns>
    Task<ContactCenterProcessedEvent> FindByHandlerAndEventAsync(
        string handlerId,
        string eventId,
        CancellationToken cancellationToken = default);
}
