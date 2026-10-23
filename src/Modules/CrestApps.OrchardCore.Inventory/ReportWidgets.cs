using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Inventory.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Inventory;

/// <summary>
/// Adds the ready-made stock widgets to the admin dashboard when the admin dashboard and reports are on.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class InventoryReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<InventoryReportWidgetMigrations>();
}
