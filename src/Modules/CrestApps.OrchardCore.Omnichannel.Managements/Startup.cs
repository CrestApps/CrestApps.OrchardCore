using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Events;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Tasks;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTask, AutomatedActivitiesProcessorBackgroundTask>();
        services
            .AddDisplayDriver<OmnichannelActivityBatch, OmnichannelActivityBatchDisplayDriver>()
            .AddScoped<ICatalog<OmnichannelActivityBatch>, OmnichannelActivityBatchCatalog>()
            .AddScoped<ICatalogEntryHandler<OmnichannelActivityBatch>, OmnichannelActivityBatchHandler>()
            .AddIndexProvider<OmnichannelActivityBatchIndexProvider>()
            .AddDataMigration<OmnichannelActivityBatchIndexMigrations>();

        services.AddDisplayDriver<OmnichannelActivityContainer, OmnichannelActivityContainerDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, OmnichannelContactDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelDisposition, OmnichannelDispositionDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelDisposition>, OmnichannelDispositionHandler>();

        services
            .AddDisplayDriver<OmnichannelCampaign, OmnichannelCampaignDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelCampaign>, OmnichannelCampaignHandler>();

        services
            .AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>()
            .AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelChannelEndpoint>, OmnichannelChannelEndpointHandler>();

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

        services.AddActivity<TryAgainActivityTask, TryAgainActivityTaskDisplayDriver>();
        services.AddActivity<CompletedActivityEvent, CompletedActivityEventDisplayDriver>();
        services.AddActivity<SetContactCommunicationPreferenceActivityTask, SetContactCommunicationPreferenceActivityTaskDisplayDriver>();
    }
}

