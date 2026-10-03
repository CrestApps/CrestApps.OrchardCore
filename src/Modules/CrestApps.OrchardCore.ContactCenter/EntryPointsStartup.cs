using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the Contact Center Inbound Entry Points feature: the entry point catalog and its administration, the one
/// place the numbers the business owns are routed from. It carries no voice dependency; the channels it answers, and
/// their own settings, come from the features that answer them.
/// </summary>
[Feature(ContactCenterConstants.Feature.EntryPoints)]
public sealed class EntryPointsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterEntryPointStore, ContactCenterEntryPointStore>()
            .AddScoped<IContactCenterEntryPointManager, ContactCenterEntryPointManager>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterEntryPointHandler>()
            .AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterConfigurationCacheInvalidationHandler<ContactCenterEntryPoint>>()
            .AddIndexProvider<ContactCenterEntryPointIndexProvider>()
            .AddDataMigration<ContactCenterEntryPointIndexMigrations>()
            // Moves the numbers typed on entry points, chosen on a phone number or mapped on a queue onto the entry
            // points, so each number is routed from one place.
            .AddDataMigration<EntryPointAddressMigrations>();

        services.AddDisplayDriver<ContactCenterEntryPoint, ContactCenterEntryPointDisplayDriver>();
        services.AddNavigationProvider<ContactCenterEntryPointsAdminMenu>();
    }
}

/// <summary>
/// Registers the deployment steps that export the entry points owned by the entry points feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.EntryPoints)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class EntryPointsDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContactCenterEntryPointDeploymentSource, ContactCenterEntryPointDeploymentStep>();
    }
}

/// <summary>
/// Registers the recipe steps that import the entry points owned by the entry points feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.EntryPoints)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class EntryPointsRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterEntryPointStep>();
    }
}
