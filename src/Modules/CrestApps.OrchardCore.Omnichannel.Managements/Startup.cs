using CrestApps.Core;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Core;
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
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.ContentTypes.Events;
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
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCatalogs()
            .AddCatalogManagers();

        services.AddScoped<IActivityBatchLoadCoordinator, DefaultActivityBatchLoadCoordinator>();
        services.AddScoped<DefaultContactActivityBatchLoader>();

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
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelContactPartSettingsDisplayDriver>();
        services.AddContentPart<OmnichannelContactPart>()
            .UseDisplayDriver<OmnichannelContactPartDisplayDriver>();
        services.AddScoped<OmnichannelContactDefinitionService>();
        services.AddScoped<IContentDefinitionHandler, OmnichannelContactDefinitionHandler>();
        services.AddScoped<IModularTenantEvents, OmnichannelContactDefinitionTenantEvents>();

        services
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityDisplayDriver>();

        services
            .AddDisplayDriver<ListOmnichannelActivityFilter, ListOmnichannelActivityFilterDisplayDriver>()
            .AddScoped<IListOmnichannelActivityFilterHandler, ListOmnichannelActivityFilterHandler>()
            .AddScoped<IListOmnichannelActivityFilterHandler, TimeZoneListOmnichannelActivityFilterHandler>();

        services
            .AddDisplayDriver<BulkManageActivityFilter, BulkManageActivityFilterDisplayDriver>()
            .AddScoped<IBulkManageActivityFilterHandler, BulkManageActivityFilterHandler>();

        services.AddScoped<BulkActivityAdminFormOptionsProvider>();

        services
            .AddDisplayDriver<BulkManageOmnichannelActivityContainer, BulkManageActivityActionsDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelDisposition, OmnichannelDispositionDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelDisposition>, OmnichannelDispositionHandler>();

        services
            .AddDisplayDriver<OmnichannelCampaign, OmnichannelCampaignDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelCampaign>, OmnichannelCampaignHandler>();

        services
            .AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<OmnichannelChannelEndpoint>, OmnichannelChannelEndpointHandler>();

        // Subject Actions.
        services
            .AddScoped<ISourceCatalog<SubjectAction>, SubjectActionCatalog>()
            .AddScoped<ICatalog<SubjectAction>>(sp => sp.GetRequiredService<ISourceCatalog<SubjectAction>>())
            .AddDisplayDriver<SubjectAction, SubjectActionDisplayDriver>()
            .AddDisplayDriver<SubjectAction, TryAgainSubjectActionDisplayDriver>()
            .AddDisplayDriver<SubjectAction, NewActivitySubjectActionDisplayDriver>()
            .AddScoped<ISubjectActionExecutor, DefaultSubjectActionExecutor>()
            .AddScoped<IActivityDispositionService, DefaultActivityDispositionService>();

        // Subject Flow Settings.
        services
            .AddDisplayDriver<SubjectFlowSettings, SubjectFlowSettingsDisplayDriver>();

        services.AddScoped<ISubjectFlowSettingsService, SubjectFlowSettingsService>();

        services.Configure<SubjectActionOptions>(options =>
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
                entry.Description = S["Creates a brand new activity, optionally targeting a different subject type."];
            });
        });

        services.Configure<ActivityBatchSourceOptions>(options =>
        {
            options.AddSource(ActivitySources.Manual, entry =>
            {
                entry.DisplayName = S["Manual"];
                entry.Description = S["Loads activities assigned to selected users for manual agent work."];
                entry.RequiresUserAssignment = true;
            });

            options.AddSource(ActivitySources.Automatic, entry =>
            {
                entry.DisplayName = S["Automatic"];
                entry.Description = S["Loads unassigned activities that AI automation processes through the configured subject flow."];
                entry.RequiresUserAssignment = false;
            });

            options.AddSource(ActivitySources.Dialer, entry =>
            {
                entry.DisplayName = S["Dialer"];
                entry.Description = S["Loads unassigned activities that dialers reserve and assign later."];
                entry.RequiresUserAssignment = false;
            });
        });

        services.AddPermissionProvider<PermissionProvider>();
        services.AddScoped<IAuthorizationHandler, OmnichannelActivityAuthorizationHandler>();
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
        routes.AddSubjectDispositionActionsEndpoint();
    }
}

[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AISubjectFlowStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<SubjectFlowSettings, AISubjectFlowSettingsDisplayDriver>();
    }
}

[RequireFeatures(ContentTransferConstants.Feature.ModuleId, PhoneNumberVerificationsConstants.Features.PhoneNumbers)]
public sealed class ContentTransferStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPartImportHandler<OmnichannelContactPart, OmnichannelContactPartContentImportHandler>();
        services.AddScoped<IOmnichannelContactDuplicateLookupService, OmnichannelContactDuplicateLookupService>();
        services.AddScoped<IContentImportRowFilter, OmnichannelContactImportRowFilter>();
        services.AddScoped<IDisplayDriver<ImportContent>, OmnichannelContactImportOptionsDisplayDriver>();
    }
}

[RequireFeatures("CrestApps.OrchardCore.DncRegistry", ContentTransferConstants.Feature.ModuleId)]
public sealed class NationalDoNotCallRegistryContentTransferStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<ImportContent, NationalDoNotCallRegistryImportOptionsDisplayDriver>();
    }
}

/// <summary>
/// Registers the Omnichannel CRM reports contributed to the admin Reports area.
/// </summary>
[Feature(OmnichannelConstants.Features.Reports)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IReport, ActivitySummaryReportProvider>()
            .AddScoped<IReport, CampaignPerformanceReportProvider>()
            .AddScoped<IReport, DispositionBreakdownReportProvider>();
    }
}
