using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Surveys.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// With Reports and the admin dashboard on, the Survey Activity widget.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class ReportWidgetsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<SurveyReportWidgetMigrations>();
}
