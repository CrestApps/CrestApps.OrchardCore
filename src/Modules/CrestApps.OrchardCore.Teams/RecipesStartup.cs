using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Teams.Deployments;
using CrestApps.OrchardCore.Teams.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Imports the Teams recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<TeamsRecipeStep>();
    }
}

/// <summary>
/// Exports the teams with a deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TeamsDeploymentSource, TeamsDeploymentStep, TeamsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the Teams recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, TeamsRecipeStepSchema>();
        services.AddDeploymentStepSchema<TeamsDeploymentStepSchema>();
    }
}
