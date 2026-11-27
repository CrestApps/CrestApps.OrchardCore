using CrestApps.OrchardCore.Geo.BackgroundTasks;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Drivers;
using CrestApps.OrchardCore.Geo.Migrations;
using CrestApps.OrchardCore.Geo.Navigation;
using CrestApps.OrchardCore.Geo.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// Registers territories, geocoding, travel times and the map settings.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(GeoConstants.CollectionName));
        services.AddGeoCore();
        services.AddPermissionProvider<GeoPermissionsProvider>();
        services
            .AddIndexProvider<ServiceTerritoryIndexProvider>()
            .AddIndexProvider<GeocodeRequestIndexProvider>()
            .AddDataMigration<GeoMigrations>();

        services.AddSiteDisplayDriver<GeoSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(GeoSettingsDisplayDriver.GroupId, GeoPermissions.ManageGeoSettings);
        services.AddDisplayDriver<TerritoryRow, TerritoryRowDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, GeocodeBackgroundTask>());
        services.AddNavigationProvider<GeoAdminMenu>();
    }
}
