using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Reviews.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reviews;

/// <summary>
/// Adds the ready-made review widget to the admin dashboard when the admin dashboard and reports are on.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class ReviewReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<ReviewReportWidgetMigrations>();
}
