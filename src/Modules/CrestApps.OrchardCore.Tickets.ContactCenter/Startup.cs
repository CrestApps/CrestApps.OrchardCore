using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tickets.ContactCenter.Drivers;
using CrestApps.OrchardCore.Tickets.ContactCenter.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.ContactCenter;

/// <summary>
/// Registers the bridge: the desk's queue, the router that offers new tickets, and the handler that gives a ticket to
/// the agent who accepts it.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<TicketDesk, TicketDeskQueueDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITicketObserver, TicketQueueRouter>());
        services.AddScoped<IContactCenterEventHandler, TicketOfferAcceptedHandler>();
    }
}
