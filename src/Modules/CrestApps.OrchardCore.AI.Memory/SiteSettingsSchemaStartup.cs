using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Memory;

/// <summary>
/// Registers the JSON schema of the AI memory site settings, so the Recipes module can describe them.
/// </summary>
[Feature(MemoryConstants.Feature.Memory)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AIMemorySiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AIMemorySettingsSchema>();
    }
}
