using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Tickets.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// Indexes a service desk settings record (a desk, status, priority, saved view or board) by id and name, in the
/// service desk's collection. Each kind of record has its own table, so each has its own index type.
/// </summary>
public abstract class TicketSettingsRecordIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// The longest name the index keeps.
    /// </summary>
    public const int NameLength = 255;

    /// <summary>
    /// Gets or sets the record's name.
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// Indexes the desks.
/// </summary>
public sealed class TicketDeskIndex : TicketSettingsRecordIndex;

/// <summary>
/// Indexes the statuses.
/// </summary>
public sealed class TicketStatusIndex : TicketSettingsRecordIndex;

/// <summary>
/// Indexes the priorities.
/// </summary>
public sealed class TicketPriorityIndex : TicketSettingsRecordIndex;

/// <summary>
/// Indexes the saved views.
/// </summary>
public sealed class TicketViewIndex : TicketSettingsRecordIndex;

/// <summary>
/// Indexes the boards.
/// </summary>
public sealed class TicketBoardIndex : TicketSettingsRecordIndex;

/// <summary>
/// Maps a kind of settings record to its index, in the service desk's collection.
/// </summary>
/// <typeparam name="T">The record.</typeparam>
/// <typeparam name="TIndex">Its index.</typeparam>
public sealed class TicketSettingsRecordIndexProvider<T, TIndex> : IndexProvider<T>
    where T : CatalogItem, ITicketSettingsRecord
    where TIndex : TicketSettingsRecordIndex, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketSettingsRecordIndexProvider{T, TIndex}"/> class.
    /// </summary>
    public TicketSettingsRecordIndexProvider()
    {
        CollectionName = TicketsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<T> context)
        => context.For<TIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(record => new TIndex
            {
                ItemId = record.ItemId,
                Name = record.Name?.Length > TicketSettingsRecordIndex.NameLength ? record.Name[..TicketSettingsRecordIndex.NameLength] : record.Name,
            });
}
