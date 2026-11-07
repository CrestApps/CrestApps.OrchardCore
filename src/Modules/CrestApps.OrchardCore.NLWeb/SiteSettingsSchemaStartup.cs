using CrestApps.OrchardCore.NLWeb.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.NLWeb;

/// <summary>
/// Registers the JSON schema of the NLWeb site settings, so CrestApps Recipes describes them in the <c>Settings</c> step.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class SiteSettingsSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, NLWebSettingsSchema>();
    }
}
