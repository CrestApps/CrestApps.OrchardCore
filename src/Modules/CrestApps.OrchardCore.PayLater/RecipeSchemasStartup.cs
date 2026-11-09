using CrestApps.OrchardCore.PayLater.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PayLater;

/// <summary>
/// Describes the pay later, bank transfer and cash on delivery settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, PayLaterSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, BankTransferSettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, CashOnDeliverySettingsSchema>();
    }
}
