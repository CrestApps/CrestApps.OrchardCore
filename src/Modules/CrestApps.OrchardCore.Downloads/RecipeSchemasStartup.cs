using CrestApps.OrchardCore.Downloads.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Downloads;

/// <summary>
/// Describes the digital files recipe step and the download settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, DigitalFilesRecipeStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, DownloadSettingsSchema>();
    }
}
