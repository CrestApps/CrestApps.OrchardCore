using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Drivers;
using CrestApps.OrchardCore.TimeTracker.Integrations;
using CrestApps.OrchardCore.TimeTracker.Migrations;
using CrestApps.OrchardCore.TimeTracker.Navigation;
using CrestApps.OrchardCore.TimeTracker.Recipes;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker;

/// <summary>
/// Registers time tracking: the stores, the service, the pages and the menu.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TimeTrackerConstants.CollectionName));
        services.AddTimeTrackerCore();
        services.AddPermissionProvider<TimeTrackerPermissionsProvider>();
        services
            .AddIndexProvider<TimeTrackerIndexProvider>()
            .AddIndexProvider<TimeCategoryIndexProvider>()
            .AddIndexProvider<TimeStageIndexProvider>()
            .AddDataMigration<TimeTrackerMigrations>();
        services.AddScoped<TimeTrackerTimeZone>();
        services.AddDisplayDriver<TimeEntryRow, TimeEntryRowDisplayDriver>();
        services.AddDisplayDriver<TimeSettingRow, TimeSettingRowDisplayDriver>();
        services.AddNavigationProvider<TimeTrackerAdminMenu>();
    }
}

/// <summary>
/// Records every change to a time entry in the Audit Trail.
/// </summary>
[RequireFeatures("OrchardCore.AuditTrail")]
public sealed class AuditTrailStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, TimeTrackerAuditTrailEvents>();
        services.AddScoped<ITimeEntryObserver, AuditTrailTimeEntryObserver>();
    }
}

/// <summary>
/// Imports stages, categories and entries from recipes.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TimeTrackerRecipeStep>();
    }
}

/// <summary>
/// Exports stages, categories and, when asked, entries.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TimeTrackerDeploymentSource, TimeTrackerDeploymentStep, TimeTrackerDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the recipe and deployment steps for the CrestApps recipes schema.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TimeTrackerRecipeStepSchema>();
        services.AddDeploymentStepSchema<TimeTrackerDeploymentStepSchema>();
    }
}
