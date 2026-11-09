using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Recipes;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers;

/// <summary>
/// Describes the customer privacy settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, CustomerPrivacySettingsSchema>();
    }
}

/// <summary>
/// Describes the customer checkout settings to CrestApps Recipes when orders are enabled.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrderConstants.Features.Orders)]
public sealed class OrdersRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, CustomerCheckoutSettingsSchema>();
    }
}

/// <summary>
/// Describes the loyalty settings to CrestApps Recipes.
/// </summary>
[Feature(CustomersConstants.Features.Loyalty)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class LoyaltyRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, LoyaltySettingsSchema>();
    }
}

/// <summary>
/// Describes the referral settings to CrestApps Recipes.
/// </summary>
[Feature(CustomersConstants.Features.Referrals)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class ReferralsRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ReferralSettingsSchema>();
    }
}
