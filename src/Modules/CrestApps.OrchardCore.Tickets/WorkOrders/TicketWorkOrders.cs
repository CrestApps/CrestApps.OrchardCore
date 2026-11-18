using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.WorkOrders;

/// <summary>
/// Turns a ticket into a work order when the request needs someone on site: the work order takes the ticket's subject,
/// first message, account and requester, remembers the ticket as its source, and the ticket links to it.
/// </summary>
public sealed class TicketWorkOrders
{
    /// <summary>
    /// The longest summary a work order gets from a ticket's subject.
    /// </summary>
    public const int MaxSummary = 120;

    private readonly ITicketService _tickets;
    private readonly ITimelineService _timeline;
    private readonly WorkOrderService _workOrders;

    internal readonly IStringLocalizer S;

    public TicketWorkOrders(ITicketService tickets, ITimelineService timeline, WorkOrderService workOrders, IStringLocalizer<TicketWorkOrders> stringLocalizer)
    {
        _tickets = tickets;
        _timeline = timeline;
        _workOrders = workOrders;
        S = stringLocalizer;
    }

    /// <summary>
    /// Creates a work order of a type from a ticket and links the ticket to it.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="typeId">The work order type.</param>
    /// <param name="priority">The priority, or <see langword="null"/> for the type's.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The work order, or why it wasn't created.</returns>
    public async Task<TicketWorkOrderResult> CreateAsync(string ticketId, string typeId, WorkOrderPriority? priority, TicketActor actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticketId) || await _tickets.GetAsync(ticketId) is not { } ticket)
        {
            return new TicketWorkOrderResult(null, [S["The ticket wasn't found."]]);
        }

        if (await _workOrders.NewAsync(typeId, cancellationToken) is not { } workOrder)
        {
            return new TicketWorkOrderResult(null, [S["Choose a work order type."]]);
        }

        Fill(workOrder, ticket, await FirstMessageAsync(ticketId, cancellationToken));

        if (priority is { } chosen)
        {
            workOrder.Priority = chosen;
        }

        var created = await _workOrders.CreateAsync(workOrder, actor?.Name, cancellationToken);

        if (created.Errors.Count > 0)
        {
            return new TicketWorkOrderResult(null, created.Errors);
        }

        // The work order is saved either way; a link that fails is reported, not undone.
        var linked = await _tickets.LinkAsync(ticketId, new RecordRef(WorkOrdersConstants.RecordKind, workOrder.ItemId), TicketLinkKinds.Related, actor, cancellationToken);

        return new TicketWorkOrderResult(workOrder, linked.Succeeded ? [] : linked.Errors);
    }

    /// <summary>
    /// Fills a new work order from a ticket.
    /// </summary>
    /// <param name="workOrder">The work order, from its type.</param>
    /// <param name="ticket">The ticket.</param>
    /// <param name="firstMessage">The ticket's first message, its description.</param>
    public static void Fill(WorkOrder workOrder, ContentItem ticket, string firstMessage)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(ticket);

        var part = ticket.GetOrCreate<TicketPart>();
        var subject = ticket.DisplayText?.Trim();
        var contact = part.RequesterContact;

        workOrder.Summary = string.IsNullOrEmpty(subject)
            ? part.Number
            : subject.Length > MaxSummary ? subject[..MaxSummary].TrimEnd() + "…" : subject;
        workOrder.Description = string.IsNullOrWhiteSpace(firstMessage) ? null : firstMessage.Trim();
        workOrder.AccountId = part.AccountId;
        workOrder.ContactId = part.Requester.Kind == PartyKinds.Contact ? part.Requester.Id : null;
        workOrder.Contact = new WorkOrderContact
        {
            Name = contact?.Name,
            Email = contact?.Email,
            Phone = contact?.Phone.HasValue == true ? contact.Phone.Value : null,
        };
        workOrder.ServiceAddress ??= contact?.Address;
        workOrder.Source = WorkOrderSource.Ticket;
        workOrder.SourceId = ticket.ContentItemId;
    }

    private async Task<string> FirstMessageAsync(string ticketId, CancellationToken cancellationToken)
    {
        // The timeline lists newest first, so the first message is the last of the ticket's public replies.
        var query = new TimelineQuery { Subject = new RecordRef(TicketsConstants.RecordKind, ticketId), Kinds = [TicketsConstants.EntryKinds.Reply], Take = 1 };
        var newest = await _timeline.ListAsync(query, cancellationToken);

        if (newest.Total <= 1)
        {
            return newest.Entries.Count > 0 ? newest.Entries[0].Body : null;
        }

        query.Skip = newest.Total - 1;

        var oldest = await _timeline.ListAsync(query, cancellationToken);

        return oldest.Entries.Count > 0 ? oldest.Entries[0].Body : null;
    }
}

/// <summary>
/// The work order made from a ticket, or why it wasn't made.
/// </summary>
/// <param name="WorkOrder">The work order, or <see langword="null"/> when it wasn't created.</param>
/// <param name="Errors">Why it wasn't created, or why the ticket couldn't link to it.</param>
public sealed record TicketWorkOrderResult(WorkOrder WorkOrder, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Gets whether the work order was created and linked.
    /// </summary>
    public bool Succeeded
        => WorkOrder is not null && Errors.Count == 0;
}
