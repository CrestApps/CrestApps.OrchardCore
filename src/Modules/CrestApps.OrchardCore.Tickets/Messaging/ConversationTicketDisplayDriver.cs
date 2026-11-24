using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Messaging;

/// <summary>
/// Adds "Open a ticket" to a messaging conversation's row, so an agent turns a conversation that needs follow-up into a
/// ticket that quotes it and links back to it.
/// </summary>
internal sealed class ConversationTicketDisplayDriver : DisplayDriver<MessagingConversation>
{
    public override IDisplayResult Display(MessagingConversation conversation, BuildDisplayContext context)
        => Initialize<ConversationTicketViewModel>("MessagingConversationTicket", model => model.ConversationId = conversation.ItemId)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:20");
}

/// <summary>
/// The conversation the "Open a ticket" link is for.
/// </summary>
public class ConversationTicketViewModel
{
    public string ConversationId { get; set; }
}
