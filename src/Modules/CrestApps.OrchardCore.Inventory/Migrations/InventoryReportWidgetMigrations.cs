using CrestApps.OrchardCore.Reports;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Inventory.Migrations;

/// <summary>
/// Defines the stock widgets.
/// </summary>
public sealed class InventoryReportWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public InventoryReportWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // The report lists the items that are out of stock first, so the first rows are the ones to reorder.
        await _contentDefinitionManager.DefineReportWidgetAsync(
            "LowStockWidget",
            "Low Stock",
            "The counted items with the least stock to sell, out of stock first.",
            new ReportWidgetPartSettings
            {
                ReportName = "stock-on-hand",
                DefaultPeriod = ReportWidgetPeriod.AllTime,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        await _contentDefinitionManager.DefineReportWidgetAsync(
            "StockMovementsWidget",
            "Stock Movements",
            "What each item received, sold, got back and was corrected by in the period.",
            new ReportWidgetPartSettings
            {
                ReportName = "stock-movements",
                DefaultPeriod = ReportWidgetPeriod.Last30Days,
                DefaultContent = ReportWidgetContent.FiguresAndTables,
                DefaultMaxRows = 5,
            });

        return 1;
    }
}
