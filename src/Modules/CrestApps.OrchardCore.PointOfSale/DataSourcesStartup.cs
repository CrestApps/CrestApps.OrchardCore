using CrestApps.OrchardCore.PointOfSale.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale;

/// <summary>
/// Exposes the registers and their shifts as a data source of Orchard Core, for reports and data pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<PointOfSaleReportDataSource>()
            .AddScoped<IPointOfSaleReportDataSet, RegistersReportDataSet>()
            .AddScoped<IPointOfSaleReportDataSet, RegisterShiftsReportDataSet>();
    }
}
