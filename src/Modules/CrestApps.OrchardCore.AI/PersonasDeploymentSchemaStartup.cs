using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Recipes.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the schema of the AI Personas deployment step, so a recipe that creates deployment plans can describe it.
/// </summary>
[Feature(AIConstants.Feature.Personas)]
[RequireFeatures("OrchardCore.Deployment", CrestAppsRecipesConstants.Feature.Area)]
public sealed class PersonasDeploymentSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeploymentStepSchema<AIPersonaDeploymentStepSchema>();
    }
}
