using CrestApps.OrchardCore.Scheduling.Deployments;
using CrestApps.OrchardCore.Scheduling.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Imports the Scheduling recipe step.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<SchedulingRecipeStep>();
    }
}

/// <summary>
/// Exports the scheduling setup with a deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SchedulingDeploymentSource, SchedulingDeploymentStep, SchedulingDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Scheduling recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CrestApps.OrchardCore.Recipes.Core.IRecipeStep, SchedulingRecipeStepSchema>();
        services.AddDeploymentStepSchema<SchedulingDeploymentStepSchema>();
    }
}
