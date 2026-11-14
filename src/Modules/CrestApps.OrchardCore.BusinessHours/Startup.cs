using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.BusinessHours.Core.Handlers;
using CrestApps.OrchardCore.BusinessHours.Deployments;
using CrestApps.OrchardCore.BusinessHours.Drivers;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Navigation;
using CrestApps.OrchardCore.BusinessHours.Recipes;
using CrestApps.OrchardCore.BusinessHours.Workflows;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.BusinessHours;

/// <summary>
/// Registers the Business Hours feature: the schedules, their administration and the schedule picker. The schedules
/// are a source of the shared open-hours service, so every module that asks "are we open?" reads them.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddCatalogManagers();

        services.AddBusinessHoursSchedules();

        // The schedules' rules come with the feature that imports them.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<BusinessHoursSchedule>, BusinessHoursScheduleHandler>());
        services.AddDisplayDriver<BusinessHoursSchedule, BusinessHoursScheduleDisplayDriver>();
        services.AddPermissionProvider<BusinessHoursPermissionsProvider>();
        services.AddNavigationProvider<BusinessHoursAdminMenu>();
    }
}

/// <summary>
/// Registers the recipe step that imports business hours schedules.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<BusinessHoursSchedulesRecipeStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports business hours schedules.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<BusinessHoursSchedulesDeploymentSource, BusinessHoursSchedulesDeploymentStep, BusinessHoursSchedulesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the Check Business Hours workflow task.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<CheckBusinessHoursTask, CheckBusinessHoursTaskDisplayDriver>();
    }
}

/// <summary>
/// Describes the Business Hours recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, BusinessHoursSchedulesRecipeStepSchema>();
        services.AddDeploymentStepSchema<BusinessHoursSchedulesDeploymentStepSchema>();
    }
}

/// <summary>
/// Describes the Check Business Hours workflow task to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<CheckBusinessHoursTaskSchema>();
    }
}
