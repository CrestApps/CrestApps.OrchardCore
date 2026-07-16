using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-backed implementation of <see cref="IContactCenterEventDeduplicationService"/>. The
/// reservation marker is staged in the ambient session so it commits atomically with the handler effect,
/// and a composite unique index over <c>HandlerId</c> and <c>EventId</c> collapses concurrent duplicates
/// to a single durable reservation.
/// </summary>
public sealed class ContactCenterEventDeduplicationService : IContactCenterEventDeduplicationService
{
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventDeduplicationService"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to read and stage reservation markers.</param>
    /// <param name="clock">The clock used to stamp the processed time.</param>
    public ContactCenterEventDeduplicationService(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> TryBeginAsync(string handlerId, string eventId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handlerId);
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        var existing = await _session
            .Query<ContactCenterProcessedEvent, ContactCenterProcessedEventIndex>(
                index => index.HandlerId == handlerId && index.EventId == eventId,
                collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return false;
        }

        var marker = new ContactCenterProcessedEvent
        {
            ItemId = IdGenerator.GenerateId(),
            HandlerId = handlerId,
            EventId = eventId,
            ProcessedUtc = _clock.UtcNow,
        };

        await _session.SaveAsync(
            marker,
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: cancellationToken);

        return true;
    }
}
