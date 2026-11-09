using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Recipes;
using CrestApps.OrchardCore.Products.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products;

/// <summary>
/// Registers recipe schema contributors for the products feature.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, ProductPartSchemaDefinition>();
    }
}

/// <summary>
/// Registers the schema of the product feed settings.
/// </summary>
[Feature(ProductConstants.Feature.Feeds)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class FeedsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<ISiteSettingsSchemaDefinition, ProductFeedSettingsSchema>();
}
