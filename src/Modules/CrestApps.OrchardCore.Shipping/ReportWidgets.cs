using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Shipping.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping;

/// <summary>
/// Adds the ready-made shipping widget to the admin dashboard when the admin dashboard and reports are on.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class ShippingReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<ShippingReportWidgetMigrations>();
}
