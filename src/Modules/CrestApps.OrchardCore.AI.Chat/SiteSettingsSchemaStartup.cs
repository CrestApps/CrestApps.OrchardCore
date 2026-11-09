using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat;

/// <summary>
/// Registers the JSON schema of the AI chat admin widget site settings, so the Recipes module can describe them.
/// </summary>
[Feature(AIConstants.Feature.ChatAdminWidget)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AIChatAdminWidgetSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AIChatAdminWidgetSettingsSchema>();
    }
}
