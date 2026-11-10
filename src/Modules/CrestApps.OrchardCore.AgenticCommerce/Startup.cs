using CrestApps.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.BackgroundTasks;
using CrestApps.OrchardCore.AgenticCommerce.Deployments;
using CrestApps.OrchardCore.AgenticCommerce.Drivers;
using CrestApps.OrchardCore.AgenticCommerce.Handlers;
using CrestApps.OrchardCore.AgenticCommerce.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Migrations;
using CrestApps.OrchardCore.AgenticCommerce.Navigation;
using CrestApps.OrchardCore.AgenticCommerce.Payments;
using CrestApps.OrchardCore.AgenticCommerce.Recipes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers agent platforms, the store's signing keys and the protocol-neutral agent checkout.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(AgenticCommerceStorage.CollectionName));

        services
            .AddScoped<IAgentPlatformStore, AgentPlatformStore>()
            .AddScoped<IAgentPlatformManager, AgentPlatformManager>()
            .AddScoped<ICatalogManager<AgentPlatform>>(serviceProvider => serviceProvider.GetRequiredService<IAgentPlatformManager>())
            .AddScoped<ICatalogEntryHandler<AgentPlatform>, AgentPlatformHandler>()
            .AddScoped<IAgentCheckoutStore, AgentCheckoutStore>()
            .AddScoped<IAgentCheckoutService, AgentCheckoutService>()
            .AddScoped<IAgentItemResolver, ProductAgentItemResolver>()
            .AddScoped<IMerchantSigningKeyService, MerchantSigningKeyService>()
            .AddScoped<AgentSiteUrls>();

        services
            .AddIndexProvider<AgentPlatformIndexProvider>()
            .AddIndexProvider<AgentCheckoutIndexProvider>()
            .AddDataMigration<AgenticCommerceMigrations>();

        services.AddDisplayDriver<AgentPlatform, AgentPlatformDisplayDriver>();
        services.AddPermissionProvider<AgenticCommercePermissionProvider>();
        services.AddSiteDisplayDriver<AgenticCommerceSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(AgenticCommerceSettingsDisplayDriver.GroupId, AgenticCommercePermissions.ManageAgenticCommerceSettings);
        services.AddNavigationProvider<AgenticCommerceAdminMenu>();

        services.AddSingleton<IBackgroundTask, AgentCheckoutExpiryBackgroundTask>();
        services.AddSingleton<IBackgroundTask, MerchantKeyRotationBackgroundTask>();

        services.AddRecipeExecutionStep<AgentPlatformsStep>();
    }
}

/// <summary>
/// Agents pay with Stripe shared payment tokens and Google Pay when the Stripe module is on.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(StripeConstants.Feature.ModuleId)]
public sealed class StripePaymentsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAgentPaymentHandler, StripeSharedPaymentTokenHandler>();
        services.AddScoped<IAgentPaymentHandler, GooglePayPaymentHandler>();
    }
}

/// <summary>
/// Agents pay with test credentials when the test payment provider is on.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(CheckoutConstants.Features.TestProvider)]
public sealed class TestPaymentsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAgentPaymentHandler, TestAgentPaymentHandler>();
    }
}

/// <summary>
/// Agent platforms travel with recipes and deployment plans, without their credentials.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AgentPlatformsDeploymentSource, AgentPlatformsDeploymentStep>();
    }
}

/// <summary>
/// Describes the agent platforms recipe step to CrestApps Recipes.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, AgentPlatformsRecipeStepSchema>();
    }
}
