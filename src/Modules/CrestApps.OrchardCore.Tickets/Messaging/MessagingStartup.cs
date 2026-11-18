using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Messaging;

/// <summary>
/// With the messaging workspace on, a conversation can be turned into a ticket.
/// </summary>
[RequireFeatures(MessagingConstants.Feature.Workspace)]
public sealed class MessagingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TicketConversationSource>();
        services.AddDisplayDriver<MessagingConversation, ConversationTicketDisplayDriver>();
    }
}
