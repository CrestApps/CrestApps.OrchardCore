using CrestApps.OrchardCore.Fundraising.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Fundraising;

/// <summary>
/// Describes the fundraising settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, FundraisingSettingsSchema>();
    }
}
