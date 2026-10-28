using CrestApps.OrchardCore.Downloads.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads;

/// <summary>
/// Exposes the download entitlements as a data source of Orchard Core, for reports and data pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<DownloadReportDataSource>()
            .AddScoped<IDownloadReportDataSet, DownloadEntitlementsReportDataSet>();
    }
}
