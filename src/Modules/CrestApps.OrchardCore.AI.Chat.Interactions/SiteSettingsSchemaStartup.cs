using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions;

/// <summary>
/// Registers the JSON schemas of the chat interaction site settings, so the Recipes module can describe them.
/// </summary>
[Feature(ChatInteractionsConstants.Feature.ChatInteractions)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AIChatInteractionsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ChatInteractionChatModeSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ChatInteractionMemorySettingsSchema>();
    }
}
