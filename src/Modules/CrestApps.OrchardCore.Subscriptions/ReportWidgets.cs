using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Adds the ready-made subscription widgets to the admin dashboard when reports can be shown there.
/// </summary>
[RequireFeatures(ReportWidgetConstants.Feature)]
public sealed class SubscriptionReportWidgetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDataMigration<SubscriptionReportWidgetMigrations>();
}
