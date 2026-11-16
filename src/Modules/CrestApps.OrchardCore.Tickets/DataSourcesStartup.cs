using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Tickets.Reports;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// Exposes tickets, their replies and notes, changes, links and daily backlog as a data source of Orchard Core, for
/// reports and data pipelines.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<TicketReportAccess>();
        services
            .AddDataSource<TicketReportDataSource>()
            .AddScoped<TicketsReportDataSet>()
            .AddScoped<TicketEntriesReportDataSet>()
            .AddScoped<TicketChangesReportDataSet>()
            .AddScoped<TicketLinksReportDataSet>()
            .AddScoped<TicketBacklogReportDataSet>()
            .AddScoped<TicketCategoriesReportDataSet>();
    }
}

/// <summary>
/// The ticket reports under Reports: volume and backlog, response and resolution times, service-level compliance,
/// agent and team performance, distribution, and the health of each account.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<TicketReportAccess>();
        services.TryAddScoped<TicketReportData>();
        services
            .AddReport<TicketVolumeReport>()
            .AddReport<TicketResponseTimesReport>()
            .AddReport<TicketServiceLevelReport>()
            .AddReport<TicketPerformanceReport>()
            .AddReport<TicketDistributionReport>()
            .AddReport<TicketAccountHealthReport>();
    }
}
