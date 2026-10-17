using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Drivers;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddDisplayDriver<ReportFilter, ContactCenterReportFilterDisplayDriver>();

        services
            .AddOptions<ContactCenterReportingOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:Reporting"))
            .Validate(
                options => options.MaximumReportRange > TimeSpan.Zero,
                "'CrestApps:ContactCenter:Reporting:MaximumReportRange' must be greater than zero.")
            .ValidateOnStart();

        services
            .AddReport<CallInsightsReportProvider>()
            .AddReport<CallQualityReportProvider>()
            .AddReport<CallHandlingReportProvider>()
            .AddReport<AgentActivityTimelineReportProvider>()
            .AddReport<ReconciledPayrollTimecardReportProvider>()
            .AddReport<AgentProductivityReportProvider>()
            .AddReport<QueueUsageReportProvider>()
            .AddReport<CampaignSummaryReportProvider>()
            .AddReport<SubjectInventoryReportProvider>();

        services.ConfigureOptions<ConfigureContactCenterReportCatalog>();
    }
}
