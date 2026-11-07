using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents;

/// <summary>
/// Registers the JSON schema of the document site settings, so the Recipes module can describe them.
/// </summary>
[Feature(ChatInteractionsConstants.Feature.ChatDocuments)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AIDocumentsSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, InteractionDocumentSettingsSchema>();
    }
}
