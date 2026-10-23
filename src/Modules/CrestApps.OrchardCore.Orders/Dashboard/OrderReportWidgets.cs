using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Orders.Dashboard.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Dashboard;

/// <summary>
/// Adds the ready-made order widgets to the admin dashboard when the admin dashboard and reports are on. Each is the report of the
/// same name, so its period, what it shows and how long it is kept are set on the widget.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class OrderReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<OrderReportWidgetMigrations>();
}
