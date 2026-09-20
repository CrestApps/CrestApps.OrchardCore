using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

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
}
