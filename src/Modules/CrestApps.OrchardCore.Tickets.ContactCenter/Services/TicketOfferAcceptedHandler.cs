using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.ContactCenter.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.ContactCenter.Services;

/// <summary>
/// Gives a ticket to the agent who accepted its offer from the queue. The assignment is conditional, checked inside the
/// ticket's lock (<see cref="TicketUpdate.OnlyIfUnassigned"/>), so a ticket someone took meanwhile stays with them, and
/// a delivery received twice changes nothing the second time. The ticket service checks that the agent may work the
/// ticket's desk (a queue's members aren't necessarily the desk's people). When the agent doesn't get the ticket, its
/// queued work is withdrawn; work the agent already took stays theirs to complete.
/// </summary>
public sealed class TicketOfferAcceptedHandler : IContactCenterEventHandler
{
    private readonly IOmnichannelActivityManager _activities;
    private readonly ITicketService _tickets;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    // The withdrawal service is resolved when used: it publishes Contact Center events through the outbox, which
    // constructs every event handler, this one among them.
    public TicketOfferAcceptedHandler(IOmnichannelActivityManager activities, ITicketService tickets, IServiceProvider services, ILogger<TicketOfferAcceptedHandler> logger)
    {
        _activities = activities;
        _tickets = tickets;
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "Tickets/ContactCenterOfferAccepted/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        if (interactionEvent?.EventType != ContactCenterConstants.Events.QueueItemAssigned)
        {
            return;
        }

        var data = interactionEvent.GetData<OfferLifecycleEventData>();
        var userId = data?.UserId;

        if (string.IsNullOrEmpty(data?.ActivityItemId)
            || string.IsNullOrEmpty(userId)
            || await _activities.FindByIdAsync(data.ActivityItemId, cancellationToken) is not { } activity
            || !activity.TryGet<TicketActivityLink>(out var link)
            || string.IsNullOrEmpty(link.TicketId)
            || await _tickets.GetAsync(link.TicketId) is not { } ticket)
        {
            return;
        }

        var assignee = ticket.GetOrCreate<TicketPart>().AssigneeId;

        if (string.Equals(assignee, userId, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrEmpty(assignee))
        {
            await WithdrawAsync(data.ActivityItemId, link.TicketId, "Someone else already has the ticket.", cancellationToken);

            return;
        }

        var result = await _tickets.UpdateAsync(link.TicketId, new TicketUpdate
        {
            AssigneeId = userId,
            OnlyIfUnassigned = true,
            Reason = "Accepted from the Contact Center queue.",
        }, new TicketActor { Party = new PartyRef(PartyKinds.User, userId), Name = TicketQueueRouter.OfferAcceptedActorName }, cancellationToken);

        if (!result.Succeeded)
        {
            // Taken meanwhile, or the agent can't work the ticket's desk.
            await WithdrawAsync(data.ActivityItemId, link.TicketId, string.Join(" ", result.Errors), cancellationToken);
        }
    }

    private async Task WithdrawAsync(string activityItemId, string ticketId, string why, CancellationToken cancellationToken)
    {
        var outcome = await _services.GetRequiredService<IQueuedWorkWithdrawalService>().WithdrawAsync(activityItemId, "The agent who accepted it didn't get the ticket: " + why, ContactCenterActor.System, cancellationToken);

        _logger.LogWarning("Ticket {TicketId} was accepted from the Contact Center queue but not given to the agent ({Why}); its queued work: {Outcome}.", ticketId, why, outcome);
    }
}
