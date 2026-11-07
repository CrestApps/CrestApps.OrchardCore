using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Recipes.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the JSON schema of the AI personas recipe step.
/// </summary>
[Feature(AIConstants.Feature.Personas)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class PersonasRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIPersonaRecipeStep>();
    }
}
