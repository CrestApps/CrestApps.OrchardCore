using CrestApps.Core.Models;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Stores a kind of service desk settings record (desks, statuses, priorities, saved views or boards) as one document per
/// record, with its index, in the service desk's collection. It is the <c>ICatalog</c> of that kind of record, so callers
/// and the catalog manager, recipe and deployment steps don't change.
/// </summary>
/// <typeparam name="T">The record.</typeparam>
/// <typeparam name="TIndex">Its index.</typeparam>
public sealed class TicketSettingsStore<T, TIndex> : DocumentCatalog<T, TIndex>
    where T : CatalogItem, ITicketSettingsRecord
    where TIndex : TicketSettingsRecordIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketSettingsStore{T, TIndex}"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public TicketSettingsStore(ISession session)
        : base(session)
    {
        CollectionName = TicketsConstants.CollectionName;
    }
}
