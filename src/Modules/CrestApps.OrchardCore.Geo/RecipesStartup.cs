using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// Imports the Geo recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<GeoRecipeStep>();
    }
}

/// <summary>
/// Exports the Geo deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<GeoDeploymentSource, GeoDeploymentStep, GeoDeploymentStepDisplayDriver>();
        services.AddSiteSettingsPropertyDeploymentStep<GeoSettings, DeploymentStartup>(
            S => S["Maps and geocoding settings"],
            S => S["The providers chosen for geocoding, travel times and routes, the built-in estimate and the map tiles. Provider keys aren't exported."]);
    }
}

/// <summary>
/// Describes the Geo recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, GeoRecipeStepSchema>();
        services.AddDeploymentStepSchema<GeoDeploymentStepSchema>();
    }
}
