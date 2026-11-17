using CrestApps.OrchardCore.Plaid.Drivers;
using CrestApps.OrchardCore.Plaid.Endpoints;
using CrestApps.OrchardCore.Plaid.Indexes;
using CrestApps.OrchardCore.Plaid.Migrations;
using CrestApps.OrchardCore.Plaid.Navigation;
using CrestApps.OrchardCore.Plaid.Recipes;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Plaid;

/// <summary>
/// Registers Plaid bank account linking: the settings, the link provider Banking shows, the kept items and the webhook.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(PlaidConstants.CollectionName));
        services.AddDataMigration<PlaidMigrations>()
            .AddIndexProvider<PlaidItemIndexProvider>();

        services.AddPlaidCore();
        services.AddHttpContextAccessor();
        services.AddScoped<IPlaidCredentialsProvider, PlaidCredentialsProvider>();
        services.AddScoped<IPlaidLinkSite, PlaidLinkSite>();

        services.AddSiteDisplayDriver<PlaidSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(PlaidConstants.SettingsGroupId, PlaidPermissions.ManagePlaidSettings);
        services.AddPermissionProvider<PlaidPermissionProvider>();
        services.AddNavigationProvider<PlaidAdminMenu>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddPlaidWebhookEndpoint();
    }
}

/// <summary>
/// Describes the Plaid settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, PlaidSettingsSchema>();
    }
}
