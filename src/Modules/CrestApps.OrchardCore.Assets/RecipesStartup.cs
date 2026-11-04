using CrestApps.OrchardCore.Assets.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// Imports the Assets recipe step.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AssetsRecipeStep>();
    }
}

/// <summary>
/// Exports the Assets deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AssetsDeploymentSource, AssetsDeploymentStep, AssetsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Assets recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AssetsRecipeStepSchema>();
        services.AddDeploymentStepSchema<AssetsDeploymentStepSchema>();
    }
}
