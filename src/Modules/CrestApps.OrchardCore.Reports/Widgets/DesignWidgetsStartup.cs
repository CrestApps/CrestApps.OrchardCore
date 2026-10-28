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
/// Registers the dashboard widget of the report builder when the admin dashboard is on: the part that shows a report or
/// a view built in the report builder, the service that runs and keeps its result, and the widget type.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures(ReportWidgetConstants.AdminDashboardFeature)]
public sealed class DesignWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ReportDesignWidgetService>();
        services.AddDataMigration<ReportDesignWidgetMigrations>();
        services.AddContentPart<ReportDesignWidgetPart>()
            .UseDisplayDriver<ReportDesignWidgetPartDisplayDriver>();
    }
}
