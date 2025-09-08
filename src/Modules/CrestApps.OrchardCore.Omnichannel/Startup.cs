using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Drivers;
using CrestApps.OrchardCore.Omnichannel.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Handlers;
using CrestApps.OrchardCore.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Migrations;
using CrestApps.OrchardCore.Omnichannel.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddIndexProvider<OmnichannelMessageIndexProvider>()
            .AddDataMigration<OmnichannelMessageIndexMigrations>();

        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(OmnichannelConstants.CollectionName));
    }
}

[Feature(OmnichannelConstants.Features.Managements)]
public sealed class ManagementsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelDisposition, OmnichannelDispositionDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelDisposition>, OmnichannelDispositionHandler>();

        services
            .AddDisplayDriver<OmnichannelCampaign, OmnichannelCampaignDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelCampaign>, OmnichannelCampaignHandler>();

        services.AddPermissionProvider<PermissionProvider>();
        services.AddNavigationProvider<AdminMenu>();

        services
            .AddIndexProvider<OmnichannelContactIndexProvider>()
            .AddDataMigration<OmnichannelContactsMigrations>();

        services.AddDataMigration<ContactMethodMigrations>();

        services.AddContentPart<PhoneNumberInfoPart>();
        services.AddContentPart<EmailInfoPart>();
        services.AddContentPart<OmnichannelContactInfoPart>();

        services
            .AddIndexProvider<OmnichannelActivityIndexProvider>()
            .AddDataMigration<OmnichannelActivityIndexMigrations>();
    }
}

[Feature(OmnichannelConstants.Features.AzureCommunicationServices)]
public sealed class AzureCommunicationServicesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // TODO: add configuration for CommunicationServiceOptions
        // Also, add display driver to manage CommunicationServiceSettings
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCommunicationServiceEndpoint();
    }
}
