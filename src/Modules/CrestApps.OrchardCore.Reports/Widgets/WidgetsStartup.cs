using CrestApps.OrchardCore.Reports.Widgets.Drivers;
using CrestApps.OrchardCore.Reports.Widgets.Migrations;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using CrestApps.OrchardCore.Reports.Widgets.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Widgets;

/// <summary>
/// Registers the report dashboard widget when the admin dashboard is on: the part that shows a report, the service that
/// runs and keeps its result, and the widget type that shows any report.
/// </summary>
[RequireFeatures(ReportWidgetConstants.AdminDashboardFeature)]
public sealed class WidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ReportWidgetService>();
        services.AddDataMigration<ReportWidgetMigrations>();
        services.AddContentPart<ReportWidgetPart>()
            .UseDisplayDriver<ReportWidgetPartDisplayDriver>();
    }
}
