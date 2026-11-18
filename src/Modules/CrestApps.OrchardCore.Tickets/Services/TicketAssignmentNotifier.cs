using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Notifications;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Tells an agent, through Orchard's notifications (the notification center and the user's notification methods), that a
/// ticket was given to them, on a desk that tells assignees. Nobody is told about a ticket they took themselves.
/// </summary>
internal sealed class TicketAssignmentNotifier : ITicketObserver
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly TicketNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public TicketAssignmentNotifier(
        ICatalog<TicketDesk> desks,
        TicketNotificationSender sender,
        IStringLocalizer<TicketAssignmentNotifier> stringLocalizer)
    {
        _desks = desks;
        _sender = sender;
        S = stringLocalizer;
    }

    public async Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
    {
        if (!ticket.TryGet<TicketPart>(out var part)
            || string.IsNullOrEmpty(part.AssigneeId)
            || await _desks.FindByIdAsync(part.DeskId, cancellationToken) is not { NotifyAssignee: true })
        {
            return;
        }

        await _sender.SendAsync(
            [part.AssigneeId],
            ticket,
            TicketsConstants.NotificationTypes.Assigned,
            title => S["A ticket was assigned to you: {0}", title],
            cancellationToken: cancellationToken);
    }
}
