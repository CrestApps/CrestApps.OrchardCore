using CrestApps.OrchardCore.Timeline.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Exposes timeline entries as a data source of Orchard Core, for reports and data pipelines.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<TimelineReportDataSource>()
            .AddScoped<TimelineEntriesReportDataSet>();
    }
}
