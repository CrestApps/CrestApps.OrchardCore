using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.Osrm;

/// <summary>
/// Registers OSRM as a travel-time and route provider.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(OsrmProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

        services.TryAddScoped<OsrmProvider>();
        services.AddScoped<ITravelTimeProvider>(sp => sp.GetRequiredService<OsrmProvider>());
        services.AddScoped<IRouteProvider>(sp => sp.GetRequiredService<OsrmProvider>());
        services.AddSiteDisplayDriver<OsrmSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(GeoConstants.SettingsGroupId, GeoPermissions.ManageGeoSettings);
    }
}
