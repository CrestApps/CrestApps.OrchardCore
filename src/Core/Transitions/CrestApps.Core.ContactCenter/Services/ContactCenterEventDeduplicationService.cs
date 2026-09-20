using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Support;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterEventDeduplicationService"/>. The
/// reservation marker is staged rather than committed, so it lands atomically with the handler effect it
/// guards, and a composite unique index over <c>HandlerId</c> and <c>EventId</c> collapses concurrent
/// duplicates to a single durable reservation.
/// </summary>
public sealed class ContactCenterEventDeduplicationService : IContactCenterEventDeduplicationService
{
    private readonly IContactCenterProcessedEventStore _store;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventDeduplicationService"/> class.
    /// </summary>
    /// <param name="store">The store the reservation markers are read from and staged in.</param>
    /// <param name="timeProvider">The time provider used to stamp the processed time.</param>
    public ContactCenterEventDeduplicationService(
        IContactCenterProcessedEventStore store,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<bool> TryBeginAsync(string handlerId, string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handlerId);
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        var existing = await _store.FindByHandlerAndEventAsync(handlerId, eventId, cancellationToken);

        if (existing is not null)
        {
            return false;
        }

        var marker = new ContactCenterProcessedEvent
        {
            ItemId = UniqueId.GenerateId(),
            HandlerId = handlerId,
            EventId = eventId,
            ProcessedUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };

        await _store.CreateAsync(marker, cancellationToken);

        return true;
    }
}
