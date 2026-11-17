using CrestApps.OrchardCore.Parties.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// A protected item of a ticket, kept apart from the ticket with its value encrypted (ASP.NET Core Data Protection, the
/// tenant's keys), so nothing that reads or sends the ticket carries it.
/// </summary>
public sealed class TicketProtectedItemDocument
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the item's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets what it is, such as "VPN login".
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the value, encrypted. Never the value itself.
    /// </summary>
    public string ProtectedValue { get; set; }

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets who added it.
    /// </summary>
    public PartyRef CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the name of who added it.
    /// </summary>
    public string CreatedByName { get; set; }

    /// <summary>
    /// Gets or sets whether the requester added it.
    /// </summary>
    public bool FromRequester { get; set; }
}

/// <summary>
/// Finds a ticket's protected items. It holds the ids only: never the label or the value.
/// </summary>
public sealed class TicketProtectedItemIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the item's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }
}

/// <summary>
/// Maps <see cref="TicketProtectedItemDocument"/> to <see cref="TicketProtectedItemIndex"/>.
/// </summary>
public sealed class TicketProtectedItemIndexProvider : IndexProvider<TicketProtectedItemDocument>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketProtectedItemIndexProvider"/> class, which maps to the service desk's collection.
    /// </summary>
    public TicketProtectedItemIndexProvider()
    {
        CollectionName = TicketsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TicketProtectedItemDocument> context)
        => context.For<TicketProtectedItemIndex>()
            .Map(item => new TicketProtectedItemIndex
            {
                ItemId = item.ItemId,
                TicketId = item.TicketId,
            });
}
