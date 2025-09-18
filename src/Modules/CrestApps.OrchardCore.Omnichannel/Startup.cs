using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Indexes;
using CrestApps.OrchardCore.Omnichannel.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddIndexProvider<OmnichannelMessageIndexProvider>()
            .AddDataMigration<OmnichannelMessageIndexMigrations>();

        services
            .AddIndexProvider<OmnichannelContactCommunicationPreferenceIndexProvider>()
            .AddDataMigration<OmnichannelContactCommunicationPreferenceIndexMigrations>();

        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(OmnichannelConstants.CollectionName));
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
