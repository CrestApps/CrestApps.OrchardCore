using CrestApps.OrchardCore.Shipping.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping;

/// <summary>
/// Exposes shipping as a data source of Orchard Core, for reports and data pipelines: the data source, and the data sets
/// of the shipments, the order lines they hold, the shipping zones and the methods each zone offers.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<ShippingReportDataSource>()
            .AddScoped<IShippingReportDataSet, ShipmentsReportDataSet>()
            .AddScoped<IShippingReportDataSet, ShipmentLinesReportDataSet>()
            .AddScoped<IShippingReportDataSet, ShippingZonesReportDataSet>()
            .AddScoped<IShippingReportDataSet, ShippingMethodsReportDataSet>();
    }
}
