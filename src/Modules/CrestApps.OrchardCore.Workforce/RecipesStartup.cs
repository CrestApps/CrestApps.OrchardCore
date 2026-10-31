using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Deployments;
using CrestApps.OrchardCore.Workforce.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Imports the Workforce recipe step.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<WorkforceRecipeStep>();
    }
}

/// <summary>
/// Exports the workforce setup with a deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<WorkforceDeploymentSource, WorkforceDeploymentStep, WorkforceDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Workforce recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, WorkforceRecipeStepSchema>();
        services.AddDeploymentStepSchema<WorkforceDeploymentStepSchema>();
    }
}
