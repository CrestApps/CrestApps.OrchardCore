using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// A ticket's link to another ticket, one row per link, so a ticket finds the tickets that link to it (a problem its
/// incidents, a parent its children).
/// </summary>
public sealed class TicketLinkIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the ticket the link is on.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the ticket it links to.
    /// </summary>
    public string LinkedTicketId { get; set; }

    /// <summary>
    /// Gets or sets how they relate, a <see cref="TicketLinkKinds"/> name.
    /// </summary>
    public string Kind { get; set; }
}

/// <summary>
/// Maps the ticket-to-ticket links of the latest version of each ticket to <see cref="TicketLinkIndex"/>.
/// </summary>
public sealed class TicketLinkIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<TicketLinkIndex>()
            .ExclusiveToDocumentType()
            .When(item => item.Latest && item.TryGet<TicketPart>(out var part) && part.Links.Count > 0)
            .Map(item => item.GetOrCreate<TicketPart>().Links
                .Where(link => link.Record.Kind == TicketsConstants.RecordKind && !string.IsNullOrEmpty(link.Record.Id))
                .Select(link => new TicketLinkIndex
                {
                    TicketId = item.ContentItemId,
                    LinkedTicketId = link.Record.Id,
                    Kind = link.Kind,
                }));
}
