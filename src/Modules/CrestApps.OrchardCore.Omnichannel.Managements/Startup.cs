using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Contents.Services;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

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

        services.AddDisplayDriver<OmnichannelActivityBatch, OmnichannelActivityBatchDisplayDriver>();

        services.AddDisplayDriver<OmnichannelActivityContainer, OmnichannelActivityContainerDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, OmnichannelContactDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelContactPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelSubjectPartSettingsDisplayDriver>();
        services.AddContentPart<OmnichannelContactPart>()
            .UseDisplayDriver<OmnichannelContactPartDisplayDriver>();

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
            .AddDisplayDriver<OmnichannelDisposition, OmnichannelDispositionDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelCampaign, OmnichannelCampaignDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelCampaignGroup, OmnichannelCampaignGroupDisplayDriver>();

        services
            .AddDisplayDriver<OmnichannelChannelEndpoint, OmnichannelChannelEndpointDisplayDriver>();

        services
            .AddDisplayDriver<SubjectAction, SubjectActionDisplayDriver>()
            .AddDisplayDriver<SubjectAction, TryAgainSubjectActionDisplayDriver>()
            .AddDisplayDriver<SubjectAction, NewActivitySubjectActionDisplayDriver>();

        services.AddNavigationProvider<AdminMenu>();

        services.AddTransient<IContentsAdminListFilterProvider, OmnichannelContactPhoneContentsAdminListFilterProvider>();

        services.AddShapeTableProvider<OmnichannelSubjectButtonsShapeTableProvider>();
        services.AddShapeTableProvider<OmnichannelSubjectPartIndexSettingsShapeTableProvider>();
    }

}

[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AISubjectFlowStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelSubjectAISettingsDisplayDriver>()
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
            serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaignGroup>>(),
            serviceProvider.GetRequiredService<INamedCatalogManager<OmnichannelDisposition>>(),
            definition,
            serviceProvider.GetRequiredService<IStringLocalizer<EnterpriseActivityReportProvider>>()));
    }
}
