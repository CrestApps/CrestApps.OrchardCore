using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterProcessedEventStore"/>.
/// </summary>
public sealed class ContactCenterProcessedEventStore : ConcurrentDocumentCatalog<ContactCenterProcessedEvent, ContactCenterProcessedEventIndex>, IContactCenterProcessedEventStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterProcessedEventStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterProcessedEvent> FindByHandlerAndEventAsync(
        string handlerId,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handlerId);
        ArgumentException.ThrowIfNullOrEmpty(eventId);

        return await Session.Query<ContactCenterProcessedEvent, ContactCenterProcessedEventIndex>(
            index => index.HandlerId == handlerId && index.EventId == eventId,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
