using CrestApps.Core;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.AI.Core.Services;
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
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Contents.Services;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

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
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

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
            .AddScoped<IActivityDispositionService, DefaultActivityDispositionService>()
            .AddScoped<IAutomatedActivityCompletionService, AutomatedActivityCompletionService>();

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
                entry.ShowInCreationPicker = false;
            });
        });

        services.AddPermissionProvider<PermissionProvider>();
        services.AddScoped<IAuthorizationHandler, OmnichannelActivityAuthorizationHandler>();
        services.AddNavigationProvider<AdminMenu>();

        services
            .AddIndexProvider<OmnichannelContactIndexProvider>()
            .AddDataMigration<OmnichannelContactsMigrations>();

        services.AddTransient<IContentsAdminListFilterProvider, OmnichannelContactPhoneContentsAdminListFilterProvider>();

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
        services
            .AddDisplayDriver<SubjectFlowSettings, AISubjectFlowSettingsDisplayDriver>()
            .AddDisplayDriver<OmnichannelActivityBatch, OmnichannelActivityBatchAIProfileDisplayDriver>()
            .AddScoped<IAIChatSessionAccessProvider, OmnichannelAIChatSessionAccessProvider>()
            .AddScoped<IAutomatedVoiceActivitySettingsResolver, AutomatedVoiceActivitySettingsResolver>();
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
/// Registers the Omnichannel reports contributed to the admin Reports area.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public ReportsStartup(IStringLocalizer<ReportsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IReport, ActivitySummaryReportProvider>()
            .AddScoped<IReport, CampaignPerformanceReportProvider>()
            .AddScoped<IReport, DispositionBreakdownReportProvider>();
        services.AddDisplayDriver<ReportFilter, OmnichannelReportFilterDisplayDriver>();

        AddEnterpriseReport(services, "omnichannel-activity-backlog", () => S["Activity backlog"], () => S["Open CRM activity inventory, assignment, reservation, and overdue workload."], EnterpriseActivityReportKind.Backlog, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-activity-aging", () => S["Activity aging"], () => S["Open activity workload grouped into enterprise aging bands."], EnterpriseActivityReportKind.Aging, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-source-performance", () => S["Activity source performance"], () => S["Activity progress and attempts grouped by the source that created or drives the work."], EnterpriseActivityReportKind.SourcePerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-channel-performance", () => S["CRM channel performance"], () => S["Activity progress and attempts grouped by communications channel."], EnterpriseActivityReportKind.ChannelPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-kind-performance", () => S["Activity kind performance"], () => S["Activity progress and attempts grouped by business work kind."], EnterpriseActivityReportKind.KindPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-assignment-performance", () => S["Activity assignment performance"], () => S["Activity progress and attempts grouped by assignment lifecycle status."], EnterpriseActivityReportKind.AssignmentPerformance, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-attempt-analysis", () => S["Activity attempt analysis"], () => S["Activity outcomes grouped by number of contact or processing attempts."], EnterpriseActivityReportKind.AttemptAnalysis, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-contact-type-workload", () => S["Contact type workload"], () => S["Activity progress and attempts grouped by CRM contact content type."], EnterpriseActivityReportKind.ContactTypeWorkload, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-urgency-performance", () => S["Activity urgency performance"], () => S["Activity progress and attempts grouped by urgency level."], EnterpriseActivityReportKind.UrgencyPerformance, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-assigned-user-performance", () => S["Assigned user performance"], () => S["Activity volume, progress, completion rate, and attempts grouped by assigned user."], EnterpriseActivityReportKind.AssignedUserPerformance, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-created-by-performance", () => S["Activity creation by user"], () => S["Activity volume and outcomes grouped by the user or system actor that created the work."], EnterpriseActivityReportKind.CreatedByPerformance, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "omnichannel-user-completion-time", () => S["User completion time"], () => S["Completed activity cycle time by assigned user, including average, median, and maximum."], EnterpriseActivityReportKind.UserCompletionTime, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-user-daily-productivity", () => S["Daily user productivity"], () => S["Completed activity count, cycle time, and attempts by assigned user and UTC day."], EnterpriseActivityReportKind.UserDailyProductivity, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-campaign-source-mix", () => S["Campaign source mix"], () => S["Campaign activity volume and outcomes by activity source."], EnterpriseActivityReportKind.CampaignSourceMix, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-campaign-channel-mix", () => S["Campaign channel mix"], () => S["Campaign activity volume and outcomes by communication channel."], EnterpriseActivityReportKind.CampaignChannelMix, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-campaign-disposition-mix", () => S["Campaign disposition mix"], () => S["Campaign activity volume and outcomes by disposition."], EnterpriseActivityReportKind.CampaignDispositionMix, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-campaign-attempt-performance", () => S["Campaign attempt performance"], () => S["Campaign activity outcomes grouped by attempt count."], EnterpriseActivityReportKind.CampaignAttemptPerformance, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-overdue-by-user", () => S["Overdue workload by user"], () => S["Overdue activity count, age, and unassigned volume grouped by assigned user."], EnterpriseActivityReportKind.OverdueByUser, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-channel-endpoint-usage", () => S["Channel endpoint usage"], () => S["Activity volume, outcomes, and attempts by configured channel endpoint."], EnterpriseActivityReportKind.ChannelEndpointUsage, ReportsConstants.Categories.Technical);
        AddEnterpriseReport(services, "omnichannel-customer-workload", () => S["Customer workload"], () => S["Activity volume, outcomes, and attempts grouped by customer record."], EnterpriseActivityReportKind.CustomerWorkload, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-schedule-completion", () => S["Scheduled completion performance"], () => S["Activities completed by schedule versus late, with completion variance."], EnterpriseActivityReportKind.ScheduleCompletion, ReportsConstants.Categories.Operations);
    }

    private static void AddEnterpriseReport(
        IServiceCollection services,
        string name,
        Func<LocalizedString> displayName,
        Func<LocalizedString> description,
        EnterpriseActivityReportKind kind,
        string category)
    {
        var definition = new EnterpriseActivityReportDefinition(name, displayName, description, kind, category);

        services.AddScoped<IReport>(serviceProvider => new EnterpriseActivityReportProvider(
            serviceProvider.GetRequiredService<global::YesSql.ISession>(),
            serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>(),
            serviceProvider.GetRequiredService<INamedCatalogManager<OmnichannelDisposition>>(),
            definition,
            serviceProvider.GetRequiredService<IStringLocalizer<EnterpriseActivityReportProvider>>()));
    }
}
