using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Drivers;
using CrestApps.OrchardCore.Assets.Fleet;
using CrestApps.OrchardCore.Assets.Migrations;
using CrestApps.OrchardCore.Assets.Navigation;
using CrestApps.OrchardCore.Assets.Owners;
using CrestApps.OrchardCore.Assets.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// Registers asset types, assets and meter readings, and their pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(AssetsConstants.CollectionName));
        services.AddAssetsCore();
        services.AddPermissionProvider<AssetsPermissionsProvider>();
        services
            .AddIndexProvider<AssetTypeIndexProvider>()
            .AddIndexProvider<AssetIndexProvider>()
            .AddIndexProvider<AssetMeterReadingIndexProvider>()
            .AddDataMigration<AssetsMigrations>();

        services.AddDisplayDriver<AssetTypeRow, AssetTypeRowDisplayDriver>();
        services.AddDisplayDriver<AssetRow, AssetRowDisplayDriver>();
        services.AddNavigationProvider<AssetsAdminMenu>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddAssetOwnerEndpoints();
    }
}

/// <summary>
/// Lets users, the customers of the store, own assets: users are a party source (owner kind Customer, party kind User).
/// </summary>
[RequireFeatures("OrchardCore.Users")]
public sealed class CustomerOwnersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddUserSearchService();
        services.AddPartySource<UserPartySource>();
    }
}

/// <summary>
/// Lets CRM contacts own assets, and lists them on contact and account pages.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Omnichannel.Managements")]
public sealed class ContactOwnersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<IOmnichannelContactTypeProvider, ContentDefinitionOmnichannelContactTypeProvider>();
        services.AddPartySource<ContactPartySource>();
        services.AddScoped<IContentDisplayDriver, ContactAssetsDisplayDriver>();
    }
}

/// <summary>
/// Registers Fleet: an Equipment calendar for each schedulable asset.
/// </summary>
[Feature(AssetsConstants.Features.Fleet)]
public sealed class FleetStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<FleetFeature>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Asset>, FleetCalendarHandler>());
    }
}

/// <summary>
/// Registers van stock: an Inventory location for each asset that carries stock.
/// </summary>
[Feature(AssetsConstants.Features.Fleet)]
[RequireFeatures("CrestApps.OrchardCore.Inventory")]
public sealed class FleetInventoryStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Asset>, FleetStockLocationHandler>());
    }
}

/// <summary>
/// Shows an account's assets on its page. Accounts own assets through the party source the Accounts module registers.
/// </summary>
[RequireFeatures(AccountsConstants.Feature)]
public sealed class AccountOwnersStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDisplayDriver, AccountAssetsDisplayDriver>();
    }
}
