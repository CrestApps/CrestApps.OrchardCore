using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Registers the schema of the agent feed settings with CrestApps Recipes.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class FeedsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<ISiteSettingsSchemaDefinition, AgentFeedSettingsSchema>();
}
