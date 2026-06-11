using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.TimeZones.Deployments;
using CrestApps.OrchardCore.TimeZones.Drivers;
using CrestApps.OrchardCore.TimeZones.Handlers;
using CrestApps.OrchardCore.TimeZones.Migrations;
using CrestApps.OrchardCore.TimeZones.Models;
using CrestApps.OrchardCore.TimeZones.Recipes;
using CrestApps.OrchardCore.TimeZones.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeZones;

/// <summary>
/// Registers services and configuration for the Time Zones feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCatalogs()
            .AddCatalogManagers();

        services
            .AddDisplayDriver<TimeZoneMap, TimeZoneMapDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<TimeZoneMap>, TimeZoneMapHandler>()
            .Replace(ServiceDescriptor.Scoped<ITimeZoneSelectListProvider, MappedTimeZoneSelectListProvider>());

        services.AddPermissionProvider<PermissionProvider>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddDataMigration<TimeZoneMapMigrations>();
    }
}

/// <summary>
/// Registers recipe execution support for the Time Zones feature.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TimeZoneMapStep>();
    }
}

/// <summary>
/// Registers deployment support for the Time Zones feature.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TimeZoneMapDeploymentSource, TimeZoneMapDeploymentStep, TimeZoneMapDeploymentStepDisplayDriver>();
    }
}
