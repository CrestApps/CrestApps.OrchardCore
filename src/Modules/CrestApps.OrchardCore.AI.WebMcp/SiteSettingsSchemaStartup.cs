using CrestApps.OrchardCore.AI.WebMcp.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.WebMcp;

/// <summary>
/// Registers the JSON schema of the WebMCP site settings, so CrestApps Recipes describes them in the <c>Settings</c> step.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class SiteSettingsSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, WebMcpSettingsSchema>();
    }
}

/// <summary>
/// Registers the JSON schema of the WebMCP content part, so CrestApps Recipes describes it in content definitions and
/// content items.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Contents)]
public sealed class ContentSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, WebMcpPartSchemaDefinition>();
    }
}
