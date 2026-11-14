using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.ServiceLevels.BackgroundTasks;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Handlers;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Deployments;
using CrestApps.OrchardCore.ServiceLevels.Drivers;
using CrestApps.OrchardCore.ServiceLevels.Migrations;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Navigation;
using CrestApps.OrchardCore.ServiceLevels.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// Registers the Service Levels feature: the stores and indexes, the tracker, the monitor task and the policy pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ServiceLevelsConstants.CollectionName));
        services.AddDataMigration<ServiceLevelsMigrations>()
            .AddIndexProvider<ServiceLevelPolicyIndexProvider>()
            .AddIndexProvider<ServiceLevelTimerIndexProvider>();

        // Timers count on the shared business hours; the feature depends on Business Hours, so its schedules and the
        // schedule picker are always there.
        services.AddOpenHours();
        services.AddServiceLevelsCore();

        // The policies' rules come with the feature that imports them.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ServiceLevelPolicy>, ServiceLevelPolicyHandler>());

        services.AddDisplayDriver<ServiceLevelPolicy, ServiceLevelPolicyDisplayDriver>();
        services.AddPermissionProvider<ServiceLevelsPermissionsProvider>();
        services.AddNavigationProvider<ServiceLevelsAdminMenu>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ServiceLevelMonitorBackgroundTask>());
    }
}

/// <summary>
/// Registers the recipe step that imports service level policies.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ServiceLevelPoliciesRecipeStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports service level policies.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ServiceLevelPoliciesDeploymentSource, ServiceLevelPoliciesDeploymentStep, ServiceLevelPoliciesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Service Levels recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ServiceLevelPoliciesRecipeStepSchema>();
        services.AddDeploymentStepSchema<ServiceLevelPoliciesDeploymentStepSchema>();
    }
}
