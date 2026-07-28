using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterProcessedEventStore"/>.
/// </summary>
public sealed class ContactCenterProcessedEventStore : DocumentCatalog<ContactCenterProcessedEvent, ContactCenterProcessedEventIndex>, IContactCenterProcessedEventStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterProcessedEventStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }
}
