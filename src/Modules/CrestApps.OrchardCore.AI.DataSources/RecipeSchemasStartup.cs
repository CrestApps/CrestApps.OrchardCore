using CrestApps.OrchardCore.AI.DataSources.Recipes.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources;

/// <summary>
/// Registers the JSON schema of the AI data sources recipe step.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AIDataSourceRecipeStep>();
    }
}
