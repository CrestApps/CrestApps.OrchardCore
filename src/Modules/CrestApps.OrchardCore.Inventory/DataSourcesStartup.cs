using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory;

/// <summary>
/// Exposes the inventory as a data source of Orchard Core, for reports and data pipelines: the data source, and the data
/// sets of the stock items, their units at each location, the stock movements, the holds for orders and the locations.
/// The purchase orders and back-in-stock alerts features register the data sets of their own records, so a data set is
/// listed only while its feature is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<InventoryReportDataSource>()
            .AddScoped<IInventoryReportDataSet, StockItemsReportDataSet>()
            .AddScoped<IInventoryReportDataSet, StockLevelsReportDataSet>()
            .AddScoped<IInventoryReportDataSet, StockMovementsReportDataSet>()
            .AddScoped<IInventoryReportDataSet, StockReservationsReportDataSet>()
            .AddScoped<IInventoryReportDataSet, LocationsReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the purchase orders feature.
/// </summary>
[Feature(InventoryConstants.Features.PurchaseOrders)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class PurchaseOrdersDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IInventoryReportDataSet, PurchaseOrdersReportDataSet>()
            .AddScoped<IInventoryReportDataSet, PurchaseOrderLinesReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the back-in-stock alerts feature.
/// </summary>
[Feature(InventoryConstants.Features.StockAlerts)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class StockAlertsDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IInventoryReportDataSet, StockAlertsReportDataSet>();
    }
}
