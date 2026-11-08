using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Reports.Widgets.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Widgets;

/// <summary>
/// Describes the part of the report dashboard widget to CrestApps Recipes when the admin dashboard is on.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, ReportWidgetConstants.AdminDashboardFeature)]
public sealed class WidgetRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, ReportWidgetPartSchemaDefinition>();
    }
}
