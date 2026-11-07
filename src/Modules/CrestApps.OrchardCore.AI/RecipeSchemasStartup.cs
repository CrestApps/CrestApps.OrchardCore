using CrestApps.OrchardCore.AI.Recipes.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the JSON schemas of the AI profile, profile template, deployment and provider connection recipe steps,
/// so the Recipes module can describe them.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIProfileRecipeStep>();
        services.AddScoped<IRecipeStep, CreateAIProfileFromTemplateRecipeStep>();
        services.AddScoped<IRecipeStep, AIProfileTemplateRecipeStep>();
        services.AddScoped<IRecipeStep, AIDeploymentRecipeStep>();
        services.AddScoped<IRecipeStep, DeleteAIDeploymentsRecipeStep>();
        services.AddScoped<IRecipeStep, AIProviderConnectionsRecipeStep>();
    }
}
