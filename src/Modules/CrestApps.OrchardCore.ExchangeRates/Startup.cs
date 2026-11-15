using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.ExchangeRates.Core;
using CrestApps.OrchardCore.ExchangeRates.Core.Indexes;
using CrestApps.OrchardCore.ExchangeRates.Core.Services;
using CrestApps.OrchardCore.ExchangeRates.Drivers;
using CrestApps.OrchardCore.ExchangeRates.Migrations;
using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Navigation;
using CrestApps.OrchardCore.ExchangeRates.Recipes;
using CrestApps.OrchardCore.ExchangeRates.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Deployment;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ExchangeRates;

/// <summary>
/// Registers the exchange rates: their store and index, the admin pages, the settings, the daily update and the recipe
/// step.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddExchangeRates()
            .AddIndexProvider<ExchangeRateIndexProvider>()
            .AddDataMigration<ExchangeRateMigrations>()
            .AddPermissionProvider<ExchangeRatesPermissionProvider>()
            .AddDisplayDriver<ExchangeRate, ExchangeRateDisplayDriver>()
            .AddSiteDisplayDriver<ExchangeRateSettingsDisplayDriver>()
            .AddNavigationProvider<ExchangeRatesAdminMenu>()
            .AddRecipeExecutionStep<ExchangeRatesStep>();

        // With the recipe step, so an imported rate gets the editor's rules.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ExchangeRate>, ExchangeRateHandler>());

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ExchangeRatesConstants.CollectionName));
        services.AddSiteSettingsPermission(ExchangeRatesConstants.SettingsGroupId, ExchangeRatesPermissions.ManageExchangeRates);
        services.AddSingleton<IBackgroundTask, ExchangeRateUpdateBackgroundTask>();
    }
}

[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDeployment<ExchangeRatesDeploymentSource, ExchangeRatesDeploymentStep>();
}

[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ExchangeRatesRecipeStepSchema>();
        services.AddDeploymentStepSchema<ExchangeRatesDeploymentStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, ExchangeRateSettingsSchema>();
    }
}
