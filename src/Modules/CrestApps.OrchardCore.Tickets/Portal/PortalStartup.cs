using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// Registers the requesters' portal: the Requests page in My account and the link in requesters' e-mails.
/// </summary>
[Feature(TicketsConstants.Features.Portal)]
public sealed class PortalStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSignedLinks();
        services.AddScoped<TicketPortalAccess>();
        services.AddScoped<ITicketRequesterLinks, TicketPortalLinks>();
        services.AddNavigationProvider<TicketsAccountMenu>();
        services.AddSingleton<IBackgroundTask, BackgroundTasks.TicketRatingRequestBackgroundTask>();
        services.AddScoped<IShapePlacementProvider, TicketCustomerPlacementProvider>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, TicketFieldCustomerSettingsDisplayDriver>();
    }
}
