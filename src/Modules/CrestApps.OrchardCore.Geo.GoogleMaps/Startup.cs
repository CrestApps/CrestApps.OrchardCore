using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.GoogleMaps;

/// <summary>
/// Registers Google Maps as a geocoding, travel-time and route provider.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(GoogleMapsProvider.HttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://maps.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

        services.TryAddScoped<GoogleMapsProvider>();
        services.AddScoped<IGeocodingProvider>(sp => sp.GetRequiredService<GoogleMapsProvider>());
        services.AddScoped<ITravelTimeProvider>(sp => sp.GetRequiredService<GoogleMapsProvider>());
        services.AddScoped<IRouteProvider>(sp => sp.GetRequiredService<GoogleMapsProvider>());
        services.AddSiteDisplayDriver<GoogleMapsSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(GeoConstants.SettingsGroupId, GeoPermissions.ManageGeoSettings);
    }
}
