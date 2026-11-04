using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Recipes.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the JSON schema of the AI tool instances recipe step.
/// </summary>
[Feature(AIConstants.Feature.ToolInstances)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class ToolInstancesRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIToolInstanceRecipeStep>();
    }
}
