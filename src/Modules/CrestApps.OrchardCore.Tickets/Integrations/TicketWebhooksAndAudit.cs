using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
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
/// The audit trail events of tickets. The audit trail records what matters for security and accountability (a ticket
/// deleted, merged or moved to another desk, which changes who may see it); the ticket's timeline holds its history.
/// </summary>
internal sealed class TicketAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Tickets";
    public const string Merged = "TicketMerged";
    public const string Deleted = "TicketDeleted";
    public const string DeskChanged = "TicketDeskChanged";

    public void Configure(AuditTrailOptions options)
    {
        options.For<TicketAuditTrailEvents>(CategoryName, S => S["Tickets"])
            .WithEvent(DeskChanged, S => S["Ticket moved to another desk"], S => S["A ticket was moved to another desk, which changes who may see it."], enableByDefault: true)
            .WithEvent(Merged, S => S["Ticket merged"], S => S["A ticket was merged into another."], enableByDefault: true)
            .WithEvent(Deleted, S => S["Ticket deleted"], S => S["A ticket was deleted with its replies, notes and files."], enableByDefault: true);
    }
}

/// <summary>
/// What an audit trail event of a ticket keeps: ids, never the requester's contact details.
/// </summary>
public sealed class TicketAuditData
{
    public string TicketId { get; set; }

    public string Number { get; set; }

    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets what happened, by id: the desk moved from and to, or the ticket merged into.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets whether a workflow made the change.
    /// </summary>
    public bool FromWorkflow { get; set; }
}

/// <summary>
/// Records the security-relevant ticket events in Orchard's audit trail, with who made the change: the actor the ticket
/// service names (an agent, or a workflow), not whoever's request happened to start it.
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

    public Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default)
        => changes.FirstOrDefault(change => change.Key == TicketChangeKeys.Desk) is { } desk
            ? RecordAsync(TicketAuditTrailEvents.DeskChanged, ticket, $"{desk.FromValue} → {desk.ToValue}")
            : Task.CompletedTask;

    public Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Merged, ticket, into?.ContentItemId);

    public Task DeletedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => RecordAsync(TicketAuditTrailEvents.Deleted, ticket, null);

    private Task RecordAsync(string name, ContentItem ticket, string detail)
    {
        var actor = TicketEventActor.Current;
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = actor?.Party.Kind == PartyKinds.User ? actor.Party.Id : actor is null ? user?.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var userName = actor?.Name ?? (actor is null ? user?.Identity?.Name : null);
        ticket.TryGet<TicketPart>(out var part);

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<TicketAuditData>(
            name,
            TicketAuditTrailEvents.CategoryName,
            ticket.ContentItemId,
            userId,
            userName,
            new TicketAuditData
            {
                TicketId = ticket.ContentItemId,
                Number = part?.Number,
                DeskId = part?.DeskId,
                Detail = detail,
                FromWorkflow = actor?.FromWorkflow == true,
            }));
    }
}
