using CrestApps.Core.AI;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Tools;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Localization;
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

        services.AddScoped<ActivityHandlerDescriber>();
        services.AddScoped<AIActivityStarter>();
        services.AddDisplayDriver<OmnichannelActivityContainer, OmnichannelActivityContainerDisplayDriver>();
        services.AddScoped<IAdminListItemDisplayManager<OmnichannelActivityListEntry>, OmnichannelActivityListEntryDisplayManager>();
        services.AddScoped<IContentDisplayDriver, OmnichannelContactDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelContactPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelSubjectPartSettingsDisplayDriver>();
        services.AddContentPart<OmnichannelContactPart>()
            .UseDisplayDriver<OmnichannelContactPartDisplayDriver>()
            .UseDisplayDriver<OmnichannelContactMemoryDisplayDriver>();
        services.AddScoped<ILeadConversionRepointer, AIContactMemoryLeadConversionRepointer>();
        services.AddContactMethods();
        services.AddScoped<IContentHandler, OmnichannelContactTimeZoneHandler>();
        services.AddScoped<IContentHandler, OmnichannelContactMethodIdsHandler>();

        services
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityDisplayDriver>()
            .AddDisplayDriver<OmnichannelActivity, OmnichannelActivityHandoffSummaryDisplayDriver>();

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
            .AddDisplayDriver<Cadence, CadenceDisplayDriver>();

        services
            .AddDisplayDriver<SubjectAction, SubjectActionDisplayDriver>()
            .AddDisplayDriver<SubjectAction, TryAgainSubjectActionDisplayDriver>()
            .AddDisplayDriver<SubjectAction, NewActivitySubjectActionDisplayDriver>();

        // The rows of the Subject Flows pages and of the numbers known not to be in service.
        services
            .AddDisplayDriver<SubjectFlowEntry, SubjectFlowEntryDisplayDriver>()
            .AddDisplayDriver<SubjectActionEntry, SubjectActionEntryDisplayDriver>()
            .AddDisplayDriver<NotInServiceNumberEntry, NotInServiceNumberEntryDisplayDriver>();

        services.AddNavigationProvider<AdminMenu>();

        services.AddTransient<IContentsAdminListFilterProvider, OmnichannelContactPhoneContentsAdminListFilterProvider>();
        services.AddDisplayDriver<ContentOptionsViewModel, OmnichannelContactPhoneContentsAdminListDisplayDriver>();

        services.AddShapeTableProvider<OmnichannelSubjectButtonsShapeTableProvider>();
        services.AddShapeTableProvider<OmnichannelSubjectPartIndexSettingsShapeTableProvider>();
        services.AddShapeTableProvider<ContactListPartShapeTableProvider>();
        services.AddScoped<ContactListPartNavigationResolver>();

        // Registered outside the CRM feature because the content import options driver, which is not part of it, uses it.
        services.AddScoped<LeadSourceProvider>();
    }
}

[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AISubjectFlowStartup : StartupBase
{
    internal readonly IStringLocalizer S;
    private readonly IShellConfiguration _shellConfiguration;

    public AISubjectFlowStartup(
        IStringLocalizer<AISubjectFlowStartup> stringLocalizer,
        IShellConfiguration shellConfiguration)
    {
        S = stringLocalizer;
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, OmnichannelSubjectAISettingsDisplayDriver>()
            .AddScoped<IAIChatSessionAccessProvider, OmnichannelAIChatSessionAccessProvider>()
            .AddScoped<IAutomatedVoiceActivitySettingsResolver, AutomatedVoiceActivitySettingsResolver>();

        // The automated-activity processing tunables, and their startup validation.
        services.Configure<OmnichannelAutomationOptions>(_shellConfiguration.GetSection("CrestApps:Omnichannel:Automation"));
        services.AddSingleton<IValidateOptions<OmnichannelAutomationOptions>, OmnichannelAutomationOptionsValidator>();

        // One reply in flight per conversation. A singleton inside the tenant container, so it is shared by the
        // scoped handlers separate inbound webhooks create and isolated from every other tenant.
        services.AddSingleton<IAutomatedConversationGate, InMemoryAutomatedConversationGate>();

        // The turn a completion records its handoff decision on. Scoped, so the tool and the handler that ran
        // the completion share one instance and two concurrent conversations cannot see each other's decision.
        services.TryAddScoped<IOmnichannelHandoffTurn, OmnichannelHandoffTurn>();

        // The transfer-to-agent tool is enabled per-turn by the automated conversation handlers (not admin-
        // selectable), so it is registered but intentionally not marked Selectable.
        services.AddCoreAITool<TransferToAgentTool>(OmnichannelHandoffHelper.TransferToAgentToolName)
            .WithTitle(S["Transfer to live agent"])
            .WithDescription(S["Hands the automated conversation off to a human agent."])
            .WithCategory(S["Omnichannel"]);
    }
}

[RequireFeatures(ContentTransferConstants.Feature.ModuleId, PhoneNumberVerificationsConstants.Features.PhoneNumbers)]
public sealed class ContentTransferStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPartImportHandler<OmnichannelContactPart, OmnichannelContactPartContentImportHandler>();
        services.AddScoped<IOmnichannelContactDuplicateLookupService, OmnichannelContactDuplicateLookupService>();
        services.AddScoped<ImportRowDoNotCallFlags>();
        services.AddScoped<IContentImportRowFilter, OmnichannelContactImportRowFilter>();
        services.AddScoped<IDisplayDriver<ImportContent>, OmnichannelContactImportOptionsDisplayDriver>();
        services.AddScoped<IDisplayDriver<ExportRequest>, OmnichannelActivityExportDisplayDriver>();

        // Register the concrete handler once so the same scoped instance serves both interfaces: the batch
        // hook pre-loads the page's activities and the import handler reads them back while mapping rows.
        services.AddScoped<ContactActivityExportHandler>();
        services.AddScoped<IContentImportHandler>(sp => sp.GetRequiredService<ContactActivityExportHandler>());
        services.AddScoped<IContentExportBatchHandler>(sp => sp.GetRequiredService<ContactActivityExportHandler>());
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
    private static readonly ObjectFactory<EnterpriseActivityReportProvider> _enterpriseReportFactory =
        ActivatorUtilities.CreateFactory<EnterpriseActivityReportProvider>([typeof(EnterpriseActivityReportKind)]);

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddReport<ActivitySummaryReportProvider>()
            .AddReport<CampaignPerformanceReportProvider>()
            .AddReport<DispositionBreakdownReportProvider>()
            .AddReport<DialerCallDetailReportProvider>()
            .AddReport<HandoffContainmentReportProvider>();
        services.AddDisplayDriver<ReportFilter, OmnichannelReportFilterDisplayDriver>();

        AddEnterpriseReport(services, "omnichannel-activity-backlog", LocalizationSource.Create<ReportsStartup>("Activity backlog"), LocalizationSource.Create<ReportsStartup>("Open CRM activity inventory, assignment, reservation, and overdue workload."), EnterpriseActivityReportKind.Backlog, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-activity-aging", LocalizationSource.Create<ReportsStartup>("Activity aging"), LocalizationSource.Create<ReportsStartup>("Open activity workload grouped into enterprise aging bands."), EnterpriseActivityReportKind.Aging, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-source-performance", LocalizationSource.Create<ReportsStartup>("Activity source performance"), LocalizationSource.Create<ReportsStartup>("Activity progress and attempts grouped by the source that created or drives the work."), EnterpriseActivityReportKind.SourcePerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-channel-performance", LocalizationSource.Create<ReportsStartup>("CRM channel performance"), LocalizationSource.Create<ReportsStartup>("Activity progress and attempts grouped by communications channel."), EnterpriseActivityReportKind.ChannelPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-kind-performance", LocalizationSource.Create<ReportsStartup>("Activity kind performance"), LocalizationSource.Create<ReportsStartup>("Activity progress and attempts grouped by business work kind."), EnterpriseActivityReportKind.KindPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-assignment-performance", LocalizationSource.Create<ReportsStartup>("Activity assignment performance"), LocalizationSource.Create<ReportsStartup>("Activity progress and attempts grouped by assignment lifecycle status."), EnterpriseActivityReportKind.AssignmentPerformance, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-attempt-analysis", LocalizationSource.Create<ReportsStartup>("Activity attempt analysis"), LocalizationSource.Create<ReportsStartup>("Activity outcomes grouped by number of contact or processing attempts."), EnterpriseActivityReportKind.AttemptAnalysis, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "omnichannel-contact-type-workload", LocalizationSource.Create<ReportsStartup>("Contact type workload"), LocalizationSource.Create<ReportsStartup>("Activity progress and attempts grouped by CRM contact content type."), EnterpriseActivityReportKind.ContactTypeWorkload, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-urgency-performance", LocalizationSource.Create<ReportsStartup>("Activity urgency performance"), LocalizationSource.Create<ReportsStartup>("Activity progress and attempts grouped by urgency level."), EnterpriseActivityReportKind.UrgencyPerformance, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "omnichannel-assigned-user-performance", LocalizationSource.Create<ReportsStartup>("Assigned user performance"), LocalizationSource.Create<ReportsStartup>("Activity volume, progress, completion rate, and attempts grouped by assigned user."), EnterpriseActivityReportKind.AssignedUserPerformance, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-created-by-performance", LocalizationSource.Create<ReportsStartup>("Activity creation by user"), LocalizationSource.Create<ReportsStartup>("Activity volume and outcomes grouped by the user or system actor that created the work."), EnterpriseActivityReportKind.CreatedByPerformance, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "omnichannel-user-completion-time", LocalizationSource.Create<ReportsStartup>("User completion time"), LocalizationSource.Create<ReportsStartup>("Completed activity cycle time by assigned user, including average, median, and maximum."), EnterpriseActivityReportKind.UserCompletionTime, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-user-daily-productivity", LocalizationSource.Create<ReportsStartup>("Daily user productivity"), LocalizationSource.Create<ReportsStartup>("Completed activity count, cycle time, and attempts by assigned user and UTC day."), EnterpriseActivityReportKind.UserDailyProductivity, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-campaign-source-mix", LocalizationSource.Create<ReportsStartup>("Campaign source mix"), LocalizationSource.Create<ReportsStartup>("Campaign activity volume and outcomes by activity source."), EnterpriseActivityReportKind.CampaignSourceMix, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-campaign-channel-mix", LocalizationSource.Create<ReportsStartup>("Campaign channel mix"), LocalizationSource.Create<ReportsStartup>("Campaign activity volume and outcomes by communication channel."), EnterpriseActivityReportKind.CampaignChannelMix, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-campaign-disposition-mix", LocalizationSource.Create<ReportsStartup>("Campaign disposition mix"), LocalizationSource.Create<ReportsStartup>("Campaign activity volume and outcomes by disposition."), EnterpriseActivityReportKind.CampaignDispositionMix, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-campaign-attempt-performance", LocalizationSource.Create<ReportsStartup>("Campaign attempt performance"), LocalizationSource.Create<ReportsStartup>("Campaign activity outcomes grouped by attempt count."), EnterpriseActivityReportKind.CampaignAttemptPerformance, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-overdue-by-user", LocalizationSource.Create<ReportsStartup>("Overdue workload by user"), LocalizationSource.Create<ReportsStartup>("Overdue activity count, age, and unassigned volume grouped by assigned user."), EnterpriseActivityReportKind.OverdueByUser, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "omnichannel-channel-endpoint-usage", LocalizationSource.Create<ReportsStartup>("Address usage"), LocalizationSource.Create<ReportsStartup>("Activity volume, outcomes, and attempts by configured address."), EnterpriseActivityReportKind.ChannelEndpointUsage, ReportsConstants.Categories.Technical);
        AddEnterpriseReport(services, "omnichannel-customer-workload", LocalizationSource.Create<ReportsStartup>("Customer workload"), LocalizationSource.Create<ReportsStartup>("Activity volume, outcomes, and attempts grouped by customer record."), EnterpriseActivityReportKind.CustomerWorkload, ReportsConstants.Categories.CrmCampaigns);
        AddEnterpriseReport(services, "omnichannel-schedule-completion", LocalizationSource.Create<ReportsStartup>("Scheduled completion performance"), LocalizationSource.Create<ReportsStartup>("Activities completed by schedule versus late, with completion variance."), EnterpriseActivityReportKind.ScheduleCompletion, ReportsConstants.Categories.Operations);
    }

    private static void AddEnterpriseReport(
        IServiceCollection services,
        string name,
        LocalizationSource displayName,
        LocalizationSource description,
        EnterpriseActivityReportKind kind,
        string category)
    {
        services.Configure<ReportOptions>(options => options.Add(new ReportDescriptor(
            name,
            displayName,
            description,
            category,
            OmnichannelConstants.Permissions.ViewReports,
            typeof(EnterpriseActivityReportProvider),
            serviceProvider => _enterpriseReportFactory(serviceProvider, [kind]))));
    }
}
