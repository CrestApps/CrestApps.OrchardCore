using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Shipping.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping;

/// <summary>
/// Adds the ready-made shipping widget to the admin dashboard when reports can be shown there.
/// </summary>
[RequireFeatures(ReportWidgetConstants.Feature)]
public sealed class ShippingReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<ShippingReportWidgetMigrations>();
}
