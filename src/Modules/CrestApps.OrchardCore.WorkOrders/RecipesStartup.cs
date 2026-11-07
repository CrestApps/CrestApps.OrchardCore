using CrestApps.OrchardCore.WorkOrders.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Imports the WorkOrders recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<WorkOrdersRecipeStep>();
    }
}

/// <summary>
/// Exports the WorkOrders deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<WorkOrdersDeploymentSource, WorkOrdersDeploymentStep, WorkOrdersDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the WorkOrders recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkOrdersRecipeStepSchema>();
        services.AddDeploymentStepSchema<WorkOrdersDeploymentStepSchema>();
    }
}
