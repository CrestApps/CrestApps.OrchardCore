using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTask, AutomatedActivitiesProcessorBackgroundTask>();
        services
            .AddDisplayDriver<OmnichannelActivityBatch, OmnichannelActivityBatchDisplayDriver>()
            .AddYesSqlDocumentCatalog<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName)
            .AddScoped<IOmnichannelActivityStore, OmnichannelActivityStore>()
            .AddScoped<IOmnichannelActivityManager, OmnichannelActivityManager>()
            .AddScoped<IOmnichannelChannelEndpointStore, OmnichannelChannelEndpointStore>()
            .AddScoped<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>()
            .AddScoped<ICatalogEntryHandler<OmnichannelActivityBatch>, OmnichannelActivityBatchHandler>()
            .AddIndexProvider<OmnichannelActivityBatchIndexProvider>()
            .AddDataMigration<OmnichannelActivityBatchIndexMigrations>();

        services.AddDisplayDriver<OmnichannelActivityContainer, OmnichannelActivityContainerDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, OmnichannelContactDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityDisplayDriver>();

        services
            .AddDisplayDriver<ListOmnichannelActivityFilter, ListOmnichannelActivityFilterDisplayDriver>()
            .AddScoped<IListOmnichannelActivityFilterHandler, ListOmnichannelActivityFilterHandler>();

        services
            .AddDisplayDriver<OmnichannelDisposition, OmnichannelDispositionDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelDisposition>, OmnichannelDispositionHandler>();

        services
            .AddDisplayDriver<OmnichannelCampaign, OmnichannelCampaignDisplayDriver>()
            .AddDisplayDriver<OmnichannelCampaign, CampaignActionsListDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelCampaign>, OmnichannelCampaignHandler>();

        services
            .AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelChannelEndpoint>, OmnichannelChannelEndpointHandler>();

        // Campaign Actions.
        services
            .AddScoped<ISourceCatalog<CampaignAction>, CampaignActionCatalog>()
            .AddScoped<ICatalog<CampaignAction>>(sp => sp.GetRequiredService<ISourceCatalog<CampaignAction>>())
            .AddDisplayDriver<CampaignAction, CampaignActionDisplayDriver>()
            .AddDisplayDriver<CampaignAction, TryAgainCampaignActionDisplayDriver>()
            .AddDisplayDriver<CampaignAction, NewActivityCampaignActionDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<CampaignAction>, CampaignActionHandler>()
            .AddScoped<ICampaignActionExecutor, DefaultCampaignActionExecutor>();

        services.Configure<CampaignActionOptions>(options =>
        {
            options.AddActionType(OmnichannelConstants.ActionTypes.Finish, entry =>
            {
                entry.DisplayName = S["Finish"];
                entry.Description = S["Completes the task. No additional actions are taken."];
            });

            options.AddActionType(OmnichannelConstants.ActionTypes.TryAgain, entry =>
            {
                entry.DisplayName = S["Try Again"];
                entry.Description = S["Creates a retry activity with the same details and an incremented attempt count."];
            });

            options.AddActionType(OmnichannelConstants.ActionTypes.NewActivity, entry =>
            {
                entry.DisplayName = S["New Activity"];
                entry.Description = S["Creates a brand new activity, optionally targeting a different campaign or subject type."];
            });
        });

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

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddDispositionActionsEndpoint();
    }
}
