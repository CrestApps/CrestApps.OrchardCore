using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.Migrations;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the Calendar dashboard widget when the admin dashboard is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AdminDashboard)]
public sealed class DashboardStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<CalendarWidgetMigrations>();
        services.AddContentPart<CalendarWidgetPart>()
            .UseDisplayDriver<CalendarWidgetPartDisplayDriver>();
    }
}
