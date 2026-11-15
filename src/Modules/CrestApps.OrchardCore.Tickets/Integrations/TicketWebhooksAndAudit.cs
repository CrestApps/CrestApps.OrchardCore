using System.Security.Claims;
using CrestApps.OrchardCore.Tickets.Core.Api;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Integrations;

/// <summary>
/// The webhook events of tickets.
/// </summary>
public static class TicketWebhookEvents
{
    public const string Created = "ticket.created";
    public const string Updated = "ticket.updated";
    public const string Assigned = "ticket.assigned";
    public const string ReplyAdded = "ticket.reply_added";
    public const string Solved = "ticket.solved";
    public const string Closed = "ticket.closed";
}

/// <summary>
/// Tells webhook subscribers about tickets: created, updated, assigned, a public reply added (internal notes never leave
/// the site), solved and closed. The payload is the ticket as the API gives it.
/// </summary>
internal sealed class TicketWebhookObserver : ITicketObserver
{
    private readonly IWebhookPublisher _publisher;

    public TicketWebhookObserver(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(TicketWebhookEvents.Created, new { ticket = TicketApiModel.From(ticket) }, $"evt_ticket-created_{ticket.ContentItemId}", cancellationToken);

    public Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(TicketWebhookEvents.Updated, new { ticket = TicketApiModel.From(ticket), changed = changes.Select(change => change.Key ?? change.Field).ToArray() }, eventId: null, cancellationToken);

    public Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(TicketWebhookEvents.Assigned, new { ticket = TicketApiModel.From(ticket), previousAssigneeId }, eventId: null, cancellationToken);

    public Task RepliedAsync(ContentItem ticket, TimelineEntry entry, bool fromRequester, CancellationToken cancellationToken = default)
    {
        if (entry is null || entry.Visibility != TimelineVisibility.Public)
        {
            return Task.CompletedTask;
        }

        return _publisher.PublishAsync(
            TicketWebhookEvents.ReplyAdded,
            new { ticket = TicketApiModel.From(ticket), reply = new { id = entry.ItemId, body = entry.Body, fromRequester, createdUtc = entry.CreatedUtc, channel = entry.Channel } },
            $"evt_ticket-reply_{entry.ItemId}",
            cancellationToken);
    }

    public Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
    {
        if (previous?.Category == current?.Category)
        {
            return Task.CompletedTask;
        }

        return current?.Category switch
        {
            TicketStatusCategory.Solved => _publisher.PublishAsync(TicketWebhookEvents.Solved, new { ticket = TicketApiModel.From(ticket) }, eventId: null, cancellationToken),
            TicketStatusCategory.Closed => _publisher.PublishAsync(TicketWebhookEvents.Closed, new { ticket = TicketApiModel.From(ticket) }, eventId: null, cancellationToken),
            _ => Task.CompletedTask,
        };
    }
}

/// <summary>
/// The audit trail events of tickets.
/// </summary>
internal sealed class TicketAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Tickets";
    public const string Created = "TicketCreated";
    public const string Updated = "TicketUpdated";
    public const string Assigned = "TicketAssigned";
    public const string StatusChanged = "TicketStatusChanged";
    public const string Merged = "TicketMerged";
    public const string Deleted = "TicketDeleted";

    public void Configure(AuditTrailOptions options)
    {
        options.For<TicketAuditTrailEvents>(CategoryName, S => S["Tickets"])
            .WithEvent(Created, S => S["Ticket created"], S => S["A ticket was created."], enableByDefault: true)
            .WithEvent(Updated, S => S["Ticket changed"], S => S["A ticket's values or fields changed."], enableByDefault: true)
            .WithEvent(Assigned, S => S["Ticket assigned"], S => S["A ticket was given to another assignee."], enableByDefault: true)
            .WithEvent(StatusChanged, S => S["Ticket status changed"], S => S["A ticket moved to another status."], enableByDefault: true)
            .WithEvent(Merged, S => S["Ticket merged"], S => S["A ticket was merged into another."], enableByDefault: true)
            .WithEvent(Deleted, S => S["Ticket deleted"], S => S["A ticket was deleted with its replies, notes and files."], enableByDefault: true);
    }
}

/// <summary>
/// What an audit trail event of a ticket keeps.
/// </summary>
public sealed class TicketAuditData
{
    public TicketApiModel Ticket { get; set; }

    public string Detail { get; set; }
}

/// <summary>
/// Records ticket events in Orchard's audit trail, with who did it.
/// </summary>
internal sealed class TicketAuditTrailObserver : ITicketObserver
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketAuditTrailObserver(IAuditTrailManager auditTrailManager, IHttpContextAccessor httpContextAccessor)
    {
        _auditTrailManager = auditTrailManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Created, ticket, null);

    public Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Updated, ticket, string.Join("; ", changes.Select(change => $"{change.Field}: {change.From} → {change.To}")));

    public Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Assigned, ticket, previousAssigneeId);

    public Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.StatusChanged, ticket, $"{previous?.Name} → {current?.Name}");

    public Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Merged, ticket, into?.ContentItemId);

    public Task DeletedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Deleted, ticket, null);

    private Task RecordAsync(string name, ContentItem ticket, string detail)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<TicketAuditData>(
            name,
            TicketAuditTrailEvents.CategoryName,
            ticket.ContentItemId,
            user?.FindFirstValue(ClaimTypes.NameIdentifier),
            user?.Identity?.Name,
            new TicketAuditData { Ticket = TicketApiModel.From(ticket), Detail = detail }));
    }
}
