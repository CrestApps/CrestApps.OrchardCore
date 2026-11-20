using CrestApps.OrchardCore.Fulfillment.BackgroundTasks;
using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Drivers;
using CrestApps.OrchardCore.Fulfillment.Migrations;
using CrestApps.OrchardCore.Fulfillment.Navigation;
using CrestApps.OrchardCore.Fulfillment.Recipes;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// Registers the Fulfillment partners settings page, the record of orders sent to partners, the order card, the
/// Fulfillment requests list and the background sender.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(FulfillmentConstants.CollectionName));
        services.AddFulfillmentCore();
        services
            .AddIndexProvider<FulfillmentRequestIndexProvider>()
            .AddDataMigration<FulfillmentMigrations>();

        services.AddScoped<FulfillmentRequestText>();
        services.AddPermissionProvider<FulfillmentPermissionsProvider>();
        services.AddSiteDisplayDriver<FulfillmentPartnersDisplayDriver>();
        services.AddSiteDisplayDriver<FulfillmentSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(FulfillmentConstants.SettingsGroupId, FulfillmentPermissions.ManageFulfillmentPartners);
        services.AddDisplayDriver<Order, OrderFulfillmentDisplayDriver>();
        services.AddDisplayDriver<FulfillmentRequest, FulfillmentRequestDisplayDriver>();
        services.AddNavigationProvider<FulfillmentAdminMenu>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, FulfillmentBackgroundTask>());
    }
}

/// <summary>
/// Tells the people who ship orders when an order sent to a partner needs them, in Orchard's notification center.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(
            FulfillmentConstants.NeedsAttentionNotificationType,
            LocalizationSource.Create<NotificationsStartup>("Fulfillment needing attention"),
            type => type.Description = LocalizationSource.Create<NotificationsStartup>("When an order sent to a fulfillment partner needs a person."));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFulfillmentRequestHandler, FulfillmentNotificationHandler>());
    }
}

/// <summary>
/// Exports the fulfillment settings with a deployment; Orchard Core's Settings recipe step imports them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<FulfillmentSettings, DeploymentStartup>(
            S => S["Fulfillment settings"],
            S => S["Whether paid orders are sent to the fulfillment partners by themselves."]);
    }
}

/// <summary>
/// Describes the fulfillment settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, FulfillmentSettingsSchema>();
    }
}
