using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.ContactCenter.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.ContactCenter.Services;

/// <summary>
/// Gives a ticket to the agent who accepted its offer from the queue. Assigning is a last-write projection keyed on the
/// ticket and the agent, so a delivery received twice changes nothing the second time; a ticket someone else already
/// has is left with them.
/// </summary>
public sealed class TicketOfferAcceptedHandler : IContactCenterEventHandler
{
    private readonly IOmnichannelActivityManager _activities;
    private readonly ITicketService _tickets;
    private readonly ILogger _logger;

    public TicketOfferAcceptedHandler(IOmnichannelActivityManager activities, ITicketService tickets, ILogger<TicketOfferAcceptedHandler> logger)
    {
        _activities = activities;
        _tickets = tickets;
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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Ticket {TicketId} was offered through Contact Center but someone already has it; it stays with them.", link.TicketId);
            }

            return;
        }

        var result = await _tickets.UpdateAsync(link.TicketId, new TicketUpdate { AssigneeId = userId, Reason = "Accepted from the Contact Center queue." }, new TicketActor { Party = new PartyRef(PartyKinds.User, userId), Name = TicketQueueRouter.OfferAcceptedActorName }, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Ticket {TicketId} couldn't be given to the agent who accepted it: {Errors}", link.TicketId, string.Join(" ", result.Errors));
        }
    }
}
