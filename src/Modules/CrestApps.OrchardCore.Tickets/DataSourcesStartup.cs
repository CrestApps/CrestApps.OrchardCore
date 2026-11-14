using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
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
        services
            .AddDataSource<TicketReportDataSource>()
            .AddScoped<TicketReportAccess>()
            .AddScoped<TicketsReportDataSet>()
            .AddScoped<TicketEntriesReportDataSet>()
            .AddScoped<TicketChangesReportDataSet>()
            .AddScoped<TicketLinksReportDataSet>()
            .AddScoped<TicketBacklogReportDataSet>();
    }
}
