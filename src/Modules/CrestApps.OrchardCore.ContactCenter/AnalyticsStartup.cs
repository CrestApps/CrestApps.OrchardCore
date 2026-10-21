using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Drivers;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the reporting and analytics experience: the reporting service that aggregates interactions
/// and activities into productivity, call insights, queue usage, and campaign/subject progress reports,
/// and the Reports admin navigation. Available whenever both the Work Distribution and Reports features
/// are enabled, so no separate feature is required.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.Queues, ReportsConstants.Feature)]
public sealed class AnalyticsStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public AnalyticsStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportingService, ContactCenterReportingService>();
        services.AddScoped<IContactCenterReportCapabilityGuard, ContactCenterReportCapabilityGuard>();

        // What the dialer holds right now, read by the dialer queue inventory and the campaign summary alike, through the
        // same classification the live dashboard's dispatch card uses.
        services.AddScoped<DialerRecordClassifier>();
        services.AddScoped<IDialerQueueInventoryService, DialerQueueInventoryService>();

        services.AddDisplayDriver<ReportFilter, ContactCenterReportFilterDisplayDriver>();

        services
            .AddOptions<ContactCenterReportingOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Reporting"))
            .Validate(
                options => options.MaximumReportRange > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Reporting:MaximumReportRange' must be greater than zero.")
            .Validate(
                options => options.MaximumWorkforceReportRange > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Reporting:MaximumWorkforceReportRange' must be greater than zero.")
            .Validate(
                options => options.MaximumWorkforceExportRange > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Reporting:MaximumWorkforceExportRange' must be greater than zero.")
            .Validate(
                options => options.AgentTimeSummaryBackfill >= TimeSpan.Zero,
                "'CrestApps:ContactCenter:Reporting:AgentTimeSummaryBackfill' cannot be negative. Use zero to turn the daily agent time summaries off.")
            .Validate(
                options => options.DecisionLogQuietThreshold > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Reporting:DecisionLogQuietThreshold' must be greater than zero.")
            .ValidateOnStart();

        // The daily agent time summaries the time-based workforce reports read whole days from, and the roller that
        // keeps them up to date.
        services.AddScoped<IAgentTimeSummaryReader, AgentTimeSummaryReader>();
        services.AddScoped<AgentTimeSummaryRoller>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AgentTimeSummaryBackgroundTask>());

        services
            .AddReport<CallInsightsReportProvider>()
            .AddReport<CallQualityReportProvider>()
            .AddReport<CallHandlingReportProvider>()
            .AddReport<AgentActivityTimelineReportProvider>()
            .AddReport<AgentAvailabilityReportProvider>()
            .AddReport<ReconciledPayrollTimecardReportProvider>()
            .AddReport<AgentProductivityReportProvider>()
            .AddReport<QueueUsageReportProvider>()
            .AddReport<CampaignSummaryReportProvider>()
            .AddReport<DialerQueueInventoryReportProvider>()
            .AddReport<SubjectInventoryReportProvider>()
            .AddReport<DialerDecisionLogReportProvider>();

        services.ConfigureOptions<ConfigureContactCenterReportCatalog>();
    }
}
