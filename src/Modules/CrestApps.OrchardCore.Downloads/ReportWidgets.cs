using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Downloads.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads;

/// <summary>
/// Adds the ready-made downloads widget to the admin dashboard when the admin dashboard and reports are on.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class DownloadsReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<DownloadsReportWidgetMigrations>();
}
